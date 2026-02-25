using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Models.Responses;
using Giretra.Web.Players;
using Giretra.Web.Repositories;
using Giretra.Web.Utils;

namespace Giretra.Web.Services;

/// <summary>
/// Service for managing game sessions.
/// </summary>
public sealed class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly INotificationService _notifications;
    private readonly AiPlayerRegistry _aiRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public GameService(
        IGameRepository gameRepository,
        IRoomRepository roomRepository,
        INotificationService notifications,
        AiPlayerRegistry aiRegistry,
        IServiceProvider serviceProvider,
        ILogger<GameService> logger,
        ILoggerFactory loggerFactory)
    {
        _gameRepository = gameRepository;
        _roomRepository = roomRepository;
        _notifications = notifications;
        _aiRegistry = aiRegistry;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public GameSession? CreateGame(Room room)
    {
        var gameId = $"game_{Guid.NewGuid():N}";

        // Build client positions mapping (human players only)
        var clientPositions = new Dictionary<string, PlayerPosition>();
        var playerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>();

        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var client = room.PlayerSlots[position];
            if (client != null)
            {
                clientPositions[client.ClientId] = position;
                playerComposition[position] = new MatchPlayerInfo(position, IsBot: false, UserId: client.UserId, AiAgentType: null);
            }
            else if (room.AiSlots.TryGetValue(position, out var aiType))
            {
                playerComposition[position] = new MatchPlayerInfo(position, IsBot: true, UserId: null, AiAgentType: aiType);
            }
            else
            {
                // Unassigned slot — use strongest active bot as default
                playerComposition[position] = new MatchPlayerInfo(position, IsBot: true, UserId: null, AiAgentType: _aiRegistry.GetDefaultAgentType());
            }
        }

        // Create the game session (single instance used throughout)
        var session = new GameSession
        {
            GameId = gameId,
            RoomId = room.RoomId,
            ClientPositions = clientPositions,
            PlayerComposition = playerComposition,
            IsRanked = room.IsRanked
        };

        // Create player agents (WebApiPlayerAgent for humans, AI agent from registry for AI)
        var agents = new Dictionary<PlayerPosition, IPlayerAgent>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var client = room.PlayerSlots[position];
            if (client != null)
            {
                // Human player
                agents[position] = new WebApiPlayerAgent(
                    position,
                    client.ClientId,
                    session,
                    _notifications,
                    TimeSpan.FromSeconds(room.TurnTimerSeconds));
            }
            else if (room.AiSlots.TryGetValue(position, out var aiType))
            {
                // AI player with specified type
                agents[position] = _aiRegistry.CreateAgent(aiType, position);
            }
            else
            {
                // Unassigned slot — use strongest active bot as default
                agents[position] = _aiRegistry.CreateAgent(_aiRegistry.GetDefaultAgentType(), position);
            }
        }

        // Wrap non-human agents with resilience decorator
        var resilientLogger = _loggerFactory.CreateLogger<ResilientPlayerAgent>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            if (room.PlayerSlots[position] == null)
            {
                agents[position] = new ResilientPlayerAgent(agents[position], resilientLogger);
            }
        }

        // Wrap all agents with recording decorator
        var recorder = new ActionRecorder();
        session.ActionRecorder = recorder;
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            agents[position] = new RecordingPlayerAgent(agents[position], recorder);
        }

        // Set agents on the session
        session.PlayerAgents = agents;

        // Create the GameManager with logger
        var firstDealer = PlayerPosition.Bottom;
        var gameManagerLogger = _loggerFactory.CreateLogger<GameManager>();
        var gameManager = new GameManager(agents, firstDealer, logger: gameManagerLogger);
        session.GameManager = gameManager;

        _gameRepository.Add(session);

        // Start the game loop in the background
        var cancellationToken = session.CancellationTokenSource.Token;
        session.GameLoopTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting game {GameId}", gameId);
                await gameManager.PlayMatchAsync(cancellationToken);
                session.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Game {GameId} completed", gameId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Game {GameId} was cancelled", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game {GameId} failed with error", gameId);
            }
            finally
            {
                // Reset room status and start idle timeout so "Play Again" works
                // without waiting for database persistence to complete.
                var roomService = _serviceProvider.GetRequiredService<IRoomService>();
                roomService.ResetToWaiting(room.RoomId);
            }

            // Persist match to database after room is reset (non-blocking for Play Again)
            if (session.IsRanked && session.CompletedAt != null)
                await PersistMatchAsync(session);
        });

        return session;
    }

    public GameSession? GetGame(string gameId)
    {
        return _gameRepository.GetById(gameId);
    }

    public GameStateResponse? GetGameState(string gameId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session?.MatchState == null)
            return null;

        return MapToGameStateResponse(session);
    }

    public PlayerStateResponse? GetPlayerState(string gameId, string clientId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session?.MatchState == null)
            return null;

        var position = session.GetPositionForClient(clientId);
        if (position == null)
            return null;

        var matchState = session.MatchState;
        var deal = matchState.CurrentDeal;

        // Get player's hand (sorted by game mode, or AllTrumps with natural suit order if no mode)
        var unsortedHand = deal?.Players[position.Value].Hand ?? [];
        var hand = CardSorter.SortHand(unsortedHand, deal?.ResolvedMode);

        // Get valid actions based on pending action
        IReadOnlyList<CardResponse>? validCards = null;
        IReadOnlyList<ValidActionResponse>? validActions = null;
        PendingActionType? pendingType = null;

        var isMyTurn = session.PendingAction?.Player == position.Value;
        if (isMyTurn && session.PendingAction != null)
        {
            pendingType = session.PendingAction.ActionType;

            if (session.PendingAction.ValidCards != null)
            {
                validCards = session.PendingAction.ValidCards
                    .Select(CardResponse.FromCard)
                    .ToList();
            }

            if (session.PendingAction.ValidNegotiationActions != null)
            {
                validActions = session.PendingAction.ValidNegotiationActions
                    .Select(a => MapToValidActionResponse(a))
                    .ToList();
            }
        }

        return new PlayerStateResponse
        {
            Position = position.Value,
            Hand = hand.Select(CardResponse.FromCard).ToList(),
            IsYourTurn = isMyTurn,
            PendingActionType = pendingType,
            ValidCards = validCards,
            ValidActions = validActions,
            GameState = MapToGameStateResponse(session)
        };
    }

    public bool SubmitCut(string gameId, string clientId, int position, bool fromTop)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var pending = session.PendingAction;
        if (pending == null || pending.ActionType != PendingActionType.Cut)
            return false;

        var playerPosition = session.GetPositionForClient(clientId);
        if (playerPosition == null || playerPosition.Value != pending.Player)
            return false;

        // Validate cut position
        if (position < 6 || position > 26)
            return false;

        // Complete the pending action
        pending.CutTcs?.TrySetResult((position, fromTop));
        return true;
    }

    public bool SubmitNegotiation(string gameId, string clientId, NegotiationAction action)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var pending = session.PendingAction;
        if (pending == null || pending.ActionType != PendingActionType.Negotiate)
            return false;

        var playerPosition = session.GetPositionForClient(clientId);
        if (playerPosition == null || playerPosition.Value != pending.Player)
            return false;

        // Validate the action is one of the valid options
        if (pending.ValidNegotiationActions != null)
        {
            var isValid = pending.ValidNegotiationActions.Any(va => ActionsMatch(va, action));
            if (!isValid)
                return false;
        }

        // Complete the pending action
        pending.NegotiationTcs?.TrySetResult(action);
        return true;
    }

    public bool SubmitCardPlay(string gameId, string clientId, Card card)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var pending = session.PendingAction;
        if (pending == null || pending.ActionType != PendingActionType.PlayCard)
            return false;

        var playerPosition = session.GetPositionForClient(clientId);
        if (playerPosition == null || playerPosition.Value != pending.Player)
            return false;

        // Validate the card is one of the valid options
        if (pending.ValidCards != null && !pending.ValidCards.Contains(card))
            return false;

        // Complete the pending action
        pending.PlayCardTcs?.TrySetResult(card);
        return true;
    }

    public bool SubmitContinueDeal(string gameId, string clientId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var pending = session.PendingAction;
        if (pending == null || pending.ActionType != PendingActionType.ContinueDeal)
            return false;

        var playerPosition = session.GetPositionForClient(clientId);
        if (playerPosition == null || playerPosition.Value != pending.Player)
            return false;

        // Complete the pending action
        pending.ContinueDealTcs?.TrySetResult(true);
        return true;
    }

    public bool SubmitContinueMatch(string gameId, string clientId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var pending = session.PendingAction;
        if (pending == null || pending.ActionType != PendingActionType.ContinueMatch)
            return false;

        var playerPosition = session.GetPositionForClient(clientId);
        if (playerPosition == null || playerPosition.Value != pending.Player)
            return false;

        // Complete the pending action
        pending.ContinueMatchTcs?.TrySetResult(true);
        return true;
    }

    public WatcherStateResponse? GetWatcherState(string gameId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session?.MatchState == null)
            return null;

        var matchState = session.MatchState;
        var deal = matchState.CurrentDeal;

        // Build card counts for each player
        var cardCounts = new Dictionary<PlayerPosition, int>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            cardCounts[position] = deal?.Players[position].CardCount ?? 0;
        }

        return new WatcherStateResponse
        {
            GameState = MapToGameStateResponse(session),
            PlayerCardCounts = cardCounts
        };
    }

    private static bool ActionsMatch(NegotiationAction a, NegotiationAction b)
    {
        return (a, b) switch
        {
            (AcceptAction, AcceptAction) => true,
            (AnnouncementAction aa, AnnouncementAction ab) => aa.Mode == ab.Mode,
            (DoubleAction da, DoubleAction db) => da.TargetMode == db.TargetMode,
            (RedoubleAction ra, RedoubleAction rb) => ra.TargetMode == rb.TargetMode,
            _ => false
        };
    }

    private static ValidActionResponse MapToValidActionResponse(NegotiationAction action)
    {
        return action switch
        {
            AcceptAction => new ValidActionResponse { ActionType = "Accept", Mode = null },
            AnnouncementAction a => new ValidActionResponse { ActionType = "Announce", Mode = a.Mode },
            DoubleAction d => new ValidActionResponse { ActionType = "Double", Mode = d.TargetMode },
            RedoubleAction r => new ValidActionResponse { ActionType = "Redouble", Mode = r.TargetMode },
            _ => throw new ArgumentException($"Unknown action type: {action.GetType().Name}")
        };
    }

    private GameStateResponse MapToGameStateResponse(GameSession session)
    {
        var matchState = session.MatchState!;
        var deal = matchState.CurrentDeal;

        // Build trick responses
        IReadOnlyList<TrickResponse>? completedTricks = null;
        TrickResponse? currentTrick = null;

        if (deal?.Hand != null)
        {
            completedTricks = deal.Hand.CompletedTricks
                .Select(t => MapToTrickResponse(t, deal.Hand.GameMode))
                .ToList();

            if (deal.Hand.CurrentTrick != null)
            {
                currentTrick = MapToTrickResponse(deal.Hand.CurrentTrick, deal.Hand.GameMode);
            }
        }

        // Build negotiation history
        IReadOnlyList<NegotiationActionResponse>? negotiationHistory = null;
        if (deal?.Negotiation != null)
        {
            negotiationHistory = deal.Negotiation.Actions
                .Select(NegotiationActionResponse.FromAction)
                .ToList();
        }

        return new GameStateResponse
        {
            GameId = session.GameId,
            RoomId = session.RoomId,
            TargetScore = matchState.TargetScore,
            Team1MatchPoints = matchState.Team1MatchPoints,
            Team2MatchPoints = matchState.Team2MatchPoints,
            Dealer = matchState.CurrentDealer,
            Phase = deal?.Phase ?? DealPhase.Completed,
            CompletedDealsCount = matchState.CompletedDeals.Count,
            GameMode = deal?.ResolvedMode,
            Multiplier = deal?.Multiplier,
            CurrentTrick = currentTrick,
            CompletedTricks = completedTricks,
            Team1CardPoints = deal?.Hand?.Team1CardPoints,
            Team2CardPoints = deal?.Hand?.Team2CardPoints,
            NegotiationHistory = negotiationHistory,
            CurrentBid = deal?.Negotiation?.CurrentBid,
            IsComplete = matchState.IsComplete,
            Winner = matchState.Winner,
            PendingActionType = session.PendingAction?.ActionType,
            PendingActionPlayer = session.PendingAction?.Player,
            PendingActionTimeoutAt = session.PendingAction?.TimeoutAt,
            EloChanges = session.EloResults?.ToDictionary(
                kvp => kvp.Key,
                kvp => new EloChangeResponse
                {
                    EloBefore = kvp.Value.EloBefore,
                    EloAfter = kvp.Value.EloAfter,
                    EloChange = kvp.Value.EloChange
                })
        };
    }

    private static TrickResponse MapToTrickResponse(TrickState trick, GameMode gameMode)
    {
        PlayerPosition? winner = null;
        if (trick.IsComplete)
        {
            winner = DetermineWinner(trick, gameMode);
        }

        return new TrickResponse
        {
            Leader = trick.Leader,
            TrickNumber = trick.TrickNumber,
            PlayedCards = trick.PlayedCards
                .Select(pc => new PlayedCardResponse
                {
                    Player = pc.Player,
                    Card = CardResponse.FromCard(pc.Card)
                })
                .ToList(),
            IsComplete = trick.IsComplete,
            Winner = winner
        };
    }

    private static PlayerPosition DetermineWinner(TrickState trick, GameMode gameMode)
    {
        var trumpSuit = gameMode.GetTrumpSuit();
        var leadSuit = trick.LeadSuit!.Value;

        var winningCard = trick.PlayedCards[0];

        foreach (var playedCard in trick.PlayedCards.Skip(1))
        {
            if (IsBetter(playedCard, winningCard, leadSuit, trumpSuit, gameMode))
            {
                winningCard = playedCard;
            }
        }

        return winningCard.Player;
    }

    private static bool IsBetter(
        Core.Play.PlayedCard challenger,
        Core.Play.PlayedCard current,
        CardSuit leadSuit,
        CardSuit? trumpSuit,
        GameMode gameMode)
    {
        var challengerSuit = challenger.Card.Suit;
        var currentSuit = current.Card.Suit;

        // Trump beats non-trump
        if (trumpSuit.HasValue)
        {
            if (challengerSuit == trumpSuit && currentSuit != trumpSuit)
                return true;
            if (currentSuit == trumpSuit && challengerSuit != trumpSuit)
                return false;
        }

        // If different suits (and neither is trump, or no trump mode), lead suit wins
        if (challengerSuit != currentSuit)
        {
            if (currentSuit == leadSuit && challengerSuit != leadSuit)
                return false;
            if (challengerSuit == leadSuit && currentSuit != leadSuit)
                return true;
            return false;
        }

        // Same suit: compare strength
        return challenger.Card.GetStrength(gameMode) > current.Card.GetStrength(gameMode);
    }

    public bool RejoinPlayer(string gameId, string oldClientId, string newClientId, TimeSpan turnTimeout)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return false;

        var position = session.GetPositionForClient(oldClientId);
        if (position == null)
            return false;

        // Remap in ClientPositions
        if (!session.RemapClient(oldClientId, newClientId))
            return false;

        // Find and update the WebApiPlayerAgent (unwrap recording decorator)
        var agent = session.PlayerAgents[position.Value];
        var webAgent = UnwrapWebApiAgent(agent);
        if (webAgent != null)
        {
            webAgent.UpdateClientId(newClientId);
            _logger.LogInformation(
                "Rejoined player at {Position} in game {GameId}: {OldClientId} → {NewClientId}",
                position.Value, gameId, oldClientId, newClientId);
        }
        else
        {
            _logger.LogWarning(
                "Could not find WebApiPlayerAgent for position {Position} in game {GameId}",
                position.Value, gameId);
        }

        // If there's a pending action for this player, re-notify with new clientId
        var pending = session.PendingAction;
        if (pending != null && pending.Player == position.Value)
        {
            _ = _notifications.NotifyYourTurnAsync(
                gameId, newClientId, position.Value, pending.ActionType, pending.TimeoutAt);
        }

        return true;
    }

    private static WebApiPlayerAgent? UnwrapWebApiAgent(Core.Players.IPlayerAgent agent)
    {
        // Agent chain: RecordingPlayerAgent → ResilientPlayerAgent? → WebApiPlayerAgent
        while (agent is not WebApiPlayerAgent)
        {
            if (agent is RecordingPlayerAgent recording)
            {
                // Use reflection to access _inner field
                var innerField = typeof(RecordingPlayerAgent).GetField("_inner",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                agent = (Core.Players.IPlayerAgent?)innerField?.GetValue(recording)!;
            }
            else
            {
                // Try reflection for any decorator with _inner field
                var innerField = agent.GetType().GetField("_inner",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (innerField == null)
                    return null;
                agent = (Core.Players.IPlayerAgent?)innerField.GetValue(agent)!;
            }
        }
        return agent as WebApiPlayerAgent;
    }

    public async Task AbandonGameAsync(string gameId, PlayerPosition abandonerPosition)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null)
        {
            _logger.LogWarning("AbandonGame: session {GameId} not found", gameId);
            return;
        }

        // Skip if the match already finished naturally
        if (session.IsComplete)
        {
            _logger.LogInformation("AbandonGame: session {GameId} already complete, skipping", gameId);
            return;
        }

        _logger.LogInformation("Abandoning game {GameId}, abandoner at {Position}", gameId, abandonerPosition);

        // Cancel the game loop
        session.CancellationTokenSource.Cancel();

        // Force-complete any pending TaskCompletionSource so the game loop can unblock
        var pending = session.PendingAction;
        if (pending != null)
        {
            pending.CutTcs?.TrySetCanceled();
            pending.NegotiationTcs?.TrySetCanceled();
            pending.PlayCardTcs?.TrySetCanceled();
            pending.ContinueDealTcs?.TrySetCanceled();
            pending.ContinueMatchTcs?.TrySetCanceled();
        }

        // Wait briefly for the game loop to exit
        if (session.GameLoopTask != null)
        {
            try
            {
                await session.GameLoopTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Game loop may throw due to cancellation — that's expected
            }
        }

        session.CompletedAt = DateTime.UtcNow;

        // Persist the abandoned match (skip for unranked games)
        if (session.IsRanked)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var persistence = scope.ServiceProvider.GetRequiredService<IMatchPersistenceService>();
                await persistence.PersistAbandonedMatchAsync(session, abandonerPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist abandoned match for game {GameId}", gameId);
            }
        }

        // Notify the room
        var abandonerTeam = abandonerPosition.GetTeam();
        var winnerTeam = abandonerTeam == Core.Players.Team.Team1
            ? Core.Players.Team.Team2
            : Core.Players.Team.Team1;

        await _notifications.NotifyMatchAbandonedAsync(gameId, session.RoomId, abandonerPosition, winnerTeam);

        // Reset room to Waiting and start idle timeout
        var roomService = _serviceProvider.GetRequiredService<IRoomService>();
        roomService.ResetToWaiting(session.RoomId);
    }

    private async Task PersistMatchAsync(GameSession session)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var persistence = scope.ServiceProvider.GetRequiredService<IMatchPersistenceService>();
            await persistence.PersistCompletedMatchAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist match for game {GameId}", session.GameId);
        }
    }
}
