using System.Collections.Concurrent;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Hubs;
using Giretra.Web.Models.Events;
using Giretra.Web.Models.Responses;
using Giretra.Web.Repositories;
using Giretra.Web.Services.Elo;
using Microsoft.AspNetCore.SignalR;

namespace Giretra.Web.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR.
/// Room-scoped sends are queued on the <see cref="RoomEventDispatcher"/> and
/// delivered in order by a task of their own, so the game loop never waits on a
/// client's socket. Events are built eagerly, on the caller's thread, so they
/// describe the state at the moment they were raised.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<GameHub, IGameClient> _hubContext;
    private readonly IGameRepository _gameRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IChatService _chatService;
    private readonly RoomEventDispatcher _dispatcher;
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, bool> _lastChatStatus = new();

    public NotificationService(
        IHubContext<GameHub, IGameClient> hubContext,
        IGameRepository gameRepository,
        IServiceProvider serviceProvider,
        IChatService chatService,
        RoomEventDispatcher dispatcher,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _gameRepository = gameRepository;
        _serviceProvider = serviceProvider;
        _chatService = chatService;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    private IGameClient Room(string roomId) => _hubContext.Clients.Group($"room_{roomId}");

    private void Dispatch(string roomId, string eventName, Func<Task> send) =>
        _dispatcher.Enqueue(roomId, eventName, send);

    public Task NotifyYourTurnAsync(string gameId, string clientId, PlayerPosition position, PendingActionType actionType, DateTime timeoutAt)
    {
        var ev = new YourTurnEvent
        {
            GameId = gameId,
            Position = position,
            ActionType = actionType,
            TimeoutAt = timeoutAt
        };

        var session = _gameRepository.GetById(gameId);
        if (session == null)
            return _hubContext.Clients.Group($"client_{clientId}").YourTurn(ev);

        var roomId = session.RoomId;
        var turnEv = new PlayerTurnEvent { GameId = gameId, Position = position, ActionType = actionType, TimeoutAt = timeoutAt };

        // The personal event goes through the room's queue too: it must reach the
        // player after the CardPlayed that made it their turn.
        Dispatch(roomId, "YourTurn", () => _hubContext.Clients.Group($"client_{clientId}").YourTurn(ev));
        Dispatch(roomId, "PlayerTurn", () => Room(roomId).PlayerTurn(turnEv));
        DispatchChatStatusIfChanged(roomId);
        return Task.CompletedTask;
    }

    public Task NotifyDealStartedAsync(string gameId, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return Task.CompletedTask;

        var ev = new DealStartedEvent
        {
            GameId = gameId,
            Dealer = matchState.CurrentDealer,
            DealNumber = matchState.CompletedDeals.Count + 1
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "DealStarted", () => Room(roomId).DealStarted(ev));
        DispatchChatStatusIfChanged(roomId);
        DispatchSystemChatMessage(roomId, $"--- Deal {ev.DealNumber} started ---");
        return Task.CompletedTask;
    }

    public Task NotifyNegotiationCompletedAsync(string gameId, NegotiationState negotiationState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return Task.CompletedTask;

        var deal = matchState.CurrentDeal!;
        var ev = new NegotiationCompletedEvent
        {
            GameId = gameId,
            ResolvedMode = deal.ResolvedMode!.Value,
            AnnouncerTeam = deal.AnnouncerTeam!.Value,
            Multiplier = deal.Multiplier!.Value
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "NegotiationCompleted", () => Room(roomId).NegotiationCompleted(ev));
        DispatchChatStatusIfChanged(roomId);
        return Task.CompletedTask;
    }

    public Task NotifyDealEndedAsync(string gameId, DealResult result, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return Task.CompletedTask;

        // Compute card points breakdown for each team
        var (team1Breakdown, team2Breakdown) = ComputeCardPointsBreakdown(handState);

        var ev = new DealEndedEvent
        {
            GameId = gameId,
            GameMode = result.GameMode,
            Team1CardPoints = result.Team1CardPoints,
            Team2CardPoints = result.Team2CardPoints,
            Team1MatchPointsEarned = result.Team1MatchPoints,
            Team2MatchPointsEarned = result.Team2MatchPoints,
            Team1TotalMatchPoints = matchState.Team1MatchPoints,
            Team2TotalMatchPoints = matchState.Team2MatchPoints,
            WasSweep = result.WasSweep,
            SweepingTeam = result.SweepingTeam,
            Team1Breakdown = team1Breakdown,
            Team2Breakdown = team2Breakdown
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "DealEnded", () => Room(roomId).DealEnded(ev));
        DispatchChatStatusIfChanged(roomId);

        var sweepText = result.WasSweep ? " (SWEEP!)" : "";
        DispatchSystemChatMessage(roomId,
            $"Deal ended - Cards: {result.Team1CardPoints}-{result.Team2CardPoints}{sweepText} | Match: {matchState.Team1MatchPoints}-{matchState.Team2MatchPoints}");
        return Task.CompletedTask;
    }

    private static (CardPointsBreakdownResponse Team1, CardPointsBreakdownResponse Team2) ComputeCardPointsBreakdown(HandState handState)
    {
        var team1Points = new Dictionary<CardRank, int>
        {
            [CardRank.Jack] = 0,
            [CardRank.Nine] = 0,
            [CardRank.Ace] = 0,
            [CardRank.Ten] = 0,
            [CardRank.King] = 0,
            [CardRank.Queen] = 0
        };
        var team2Points = new Dictionary<CardRank, int>
        {
            [CardRank.Jack] = 0,
            [CardRank.Nine] = 0,
            [CardRank.Ace] = 0,
            [CardRank.Ten] = 0,
            [CardRank.King] = 0,
            [CardRank.Queen] = 0
        };

        int team1LastTrickBonus = 0;
        int team2LastTrickBonus = 0;

        for (int i = 0; i < handState.CompletedTricks.Count; i++)
        {
            var trick = handState.CompletedTricks[i];
            var isLastTrick = i == 7;

            // Determine winner of this trick
            var winner = DetermineWinner(trick, handState.GameMode);
            var winnerTeam = winner.GetTeam();

            // Add card points by rank to the winning team
            var targetPoints = winnerTeam == Team.Team1 ? team1Points : team2Points;

            foreach (var playedCard in trick.PlayedCards)
            {
                var pointValue = playedCard.Card.GetPointValue(handState.GameMode);
                if (pointValue > 0 && targetPoints.ContainsKey(playedCard.Card.Rank))
                {
                    targetPoints[playedCard.Card.Rank] += pointValue;
                }
            }

            // Add last trick bonus
            if (isLastTrick)
            {
                if (winnerTeam == Team.Team1)
                    team1LastTrickBonus = 10;
                else
                    team2LastTrickBonus = 10;
            }
        }

        var team1Total = team1Points.Values.Sum() + team1LastTrickBonus;
        var team2Total = team2Points.Values.Sum() + team2LastTrickBonus;

        return (
            new CardPointsBreakdownResponse
            {
                Jacks = team1Points[CardRank.Jack],
                Nines = team1Points[CardRank.Nine],
                Aces = team1Points[CardRank.Ace],
                Tens = team1Points[CardRank.Ten],
                Kings = team1Points[CardRank.King],
                Queens = team1Points[CardRank.Queen],
                LastTrickBonus = team1LastTrickBonus,
                Total = team1Total
            },
            new CardPointsBreakdownResponse
            {
                Jacks = team2Points[CardRank.Jack],
                Nines = team2Points[CardRank.Nine],
                Aces = team2Points[CardRank.Ace],
                Tens = team2Points[CardRank.Ten],
                Kings = team2Points[CardRank.King],
                Queens = team2Points[CardRank.Queen],
                LastTrickBonus = team2LastTrickBonus,
                Total = team2Total
            }
        );
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

    private static bool IsBetter(PlayedCard challenger, PlayedCard current, CardSuit leadSuit, CardSuit? trumpSuit, GameMode gameMode)
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
            // In same-suit comparison or when one follows lead
            if (currentSuit == leadSuit && challengerSuit != leadSuit)
                return false;
            if (challengerSuit == leadSuit && currentSuit != leadSuit)
                return true;
            // Neither is lead suit, current holder keeps it
            return false;
        }

        // Same suit: compare strength
        return challenger.Card.GetStrength(gameMode) > current.Card.GetStrength(gameMode);
    }

    public Task NotifyCardPlayedAsync(string gameId, PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return Task.CompletedTask;

        var playType = DetermineCardPlayType(player, card, handState, matchState);

        var ev = new CardPlayedEvent
        {
            GameId = gameId,
            Player = player,
            Card = CardResponse.FromCard(card),
            PlayType = playType
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "CardPlayed", () => Room(roomId).CardPlayed(ev));
        return Task.CompletedTask;
    }

    private static CardPlayType DetermineCardPlayType(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        // Find the trick containing the just-played card
        TrickState trick;
        if (handState.CurrentTrick != null && handState.CurrentTrick.PlayedCards.Any(pc => pc.Player == player && pc.Card == card))
        {
            trick = handState.CurrentTrick;
        }
        else if (handState.CompletedTricks.Count > 0)
        {
            // Trick completed — card is in the last completed trick
            trick = handState.CompletedTricks[^1];
        }
        else
        {
            return CardPlayType.Normal;
        }

        var gameMode = handState.GameMode;
        var winner = DetermineWinner(trick, gameMode);
        var isWinning = winner == player;

        // Check for Master: the played card is winning, is master, AND all remaining hand cards are master
        if (isWinning && matchState.CurrentDeal != null)
        {
            var remainingHand = matchState.CurrentDeal.Players[player].Hand;
            var allPlayedCards = CollectAllPlayedCards(handState);

            if (PlayerAgentHelper.IsMasterCard(card, gameMode, remainingHand, allPlayedCards) &&
                remainingHand.All(c => PlayerAgentHelper.IsMasterCard(c, gameMode, remainingHand, allPlayedCards)))
            {
                return CardPlayType.Master;
            }
        }

        // Under: the played card is not the current trick winner (and not a lead card)
        if (!isWinning && trick.PlayedCards.Count > 1)
        {
            return CardPlayType.Under;
        }

        return CardPlayType.Normal;
    }

    private static HashSet<Card> CollectAllPlayedCards(HandState handState)
    {
        var played = new HashSet<Card>();

        foreach (var trick in handState.CompletedTricks)
        {
            foreach (var pc in trick.PlayedCards)
            {
                played.Add(pc.Card);
            }
        }

        if (handState.CurrentTrick != null)
        {
            foreach (var pc in handState.CurrentTrick.PlayedCards)
            {
                played.Add(pc.Card);
            }
        }

        return played;
    }


    public Task NotifyTrickCompletedAsync(string gameId, TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return Task.CompletedTask;

        var ev = new TrickCompletedEvent
        {
            GameId = gameId,
            Trick = MapToTrickResponse(completedTrick, handState.GameMode, winner),
            Winner = winner,
            Team1CardPoints = handState.Team1CardPoints,
            Team2CardPoints = handState.Team2CardPoints
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "TrickCompleted", () => Room(roomId).TrickCompleted(ev));
        return Task.CompletedTask;
    }

    public async Task NotifyMatchEndedAsync(string gameId, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        // Eagerly compute Elo preview so it's available when clients refresh state
        if (session.IsRanked && session.EloResults == null && matchState.Winner != null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var eloService = scope.ServiceProvider.GetRequiredService<IEloService>();
                var preview = await eloService.PreviewMatchEloAsync(session);
                session.EloResults = preview;
                session.BumpStateVersion();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute Elo preview for game {GameId}", gameId);
            }
        }

        var ev = new MatchEndedEvent
        {
            GameId = gameId,
            Winner = matchState.Winner!.Value,
            Team1MatchPoints = matchState.Team1MatchPoints,
            Team2MatchPoints = matchState.Team2MatchPoints,
            TotalDeals = matchState.CompletedDeals.Count,
            CompletedDeals = MapToCompletedDeals(matchState)
        };

        var roomId = session.RoomId;
        Dispatch(roomId, "MatchEnded", () => Room(roomId).MatchEnded(ev));
        DispatchChatStatusIfChanged(roomId);

        var winnerLabel = matchState.Winner!.Value == Core.Players.Team.Team1 ? "Team 1" : "Team 2";
        DispatchSystemChatMessage(roomId,
            $"{winnerLabel} wins! Final score: {matchState.Team1MatchPoints}-{matchState.Team2MatchPoints}");
    }

    public Task NotifyAchievementsEarnedAsync(string gameId, string roomId)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null || session.EarnedAchievements.Count == 0) return Task.CompletedTask;

        var ev = new AchievementsEarnedEvent
        {
            GameId = gameId,
            Achievements = session.EarnedAchievements.Select(a => new AchievementEarnedDto
            {
                PlayerPosition = a.PlayerPosition,
                Code = a.Code,
                Name = a.Name,
                Category = a.Category,
                Tier = a.Tier,
                IconName = a.IconName,
                IsHidden = a.IsHidden,
                DealNumber = a.DealNumber
            }).ToList()
        };

        Dispatch(roomId, "AchievementsEarned", () => Room(roomId).AchievementsEarned(ev));
        return Task.CompletedTask;
    }

    public Task NotifyPlayerJoinedAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerJoinedEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        Dispatch(roomId, "PlayerJoined", () => Room(roomId).PlayerJoined(ev));
        return Task.CompletedTask;
    }

    public Task NotifyPlayerLeftAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerLeftEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        Dispatch(roomId, "PlayerLeft", () => Room(roomId).PlayerLeft(ev));
        return Task.CompletedTask;
    }

    public Task NotifyGameStartedAsync(string roomId, string gameId)
    {
        var ev = new GameStartedEvent
        {
            RoomId = roomId,
            GameId = gameId
        };

        Dispatch(roomId, "GameStarted", () => Room(roomId).GameStarted(ev));
        DispatchChatStatusIfChanged(roomId);
        return Task.CompletedTask;
    }

    public Task NotifyPlayerKickedAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerKickedEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        Dispatch(roomId, "PlayerKicked", () => Room(roomId).PlayerKicked(ev));
        return Task.CompletedTask;
    }

    public Task NotifySeatModeChangedAsync(string roomId, PlayerPosition position, Domain.SeatAccessMode accessMode)
    {
        var ev = new SeatModeChangedEvent
        {
            RoomId = roomId,
            Position = position,
            AccessMode = accessMode
        };

        Dispatch(roomId, "SeatModeChanged", () => Room(roomId).SeatModeChanged(ev));
        return Task.CompletedTask;
    }

    public Task NotifyMatchAbandonedAsync(string gameId, string roomId, PlayerPosition abandoner, Team winnerTeam)
    {
        var ev = new MatchAbandonedEvent
        {
            GameId = gameId,
            Abandoner = abandoner,
            WinnerTeam = winnerTeam
        };

        Dispatch(roomId, "MatchAbandoned", () => Room(roomId).MatchAbandoned(ev));
        return Task.CompletedTask;
    }

    public Task NotifyRoomIdleClosedAsync(string roomId)
    {
        var ev = new RoomIdleClosedEvent { RoomId = roomId };
        Dispatch(roomId, "RoomIdleClosed", () => Room(roomId).RoomIdleClosed(ev));
        return Task.CompletedTask;
    }

    public Task NotifyRoomResetAsync(string roomId)
    {
        var ev = new RoomResetEvent { RoomId = roomId };
        Dispatch(roomId, "RoomReset", () => Room(roomId).RoomReset(ev));
        return Task.CompletedTask;
    }

    public async Task NotifyRoomsChangedAsync()
    {
        await _hubContext.Clients.Group("lobby").RoomsChanged();
    }

    public async Task NotifyPendingFriendCountChangedAsync(Guid userId, int count)
    {
        await _hubContext.Clients.Group($"user_{userId}").PendingFriendCountChanged(new PendingFriendCountChangedEvent { Count = count });
    }

    /// <summary>
    /// Records the system message now (so sequence numbers follow the order the
    /// events were raised in) and queues its delivery.
    /// </summary>
    private void DispatchSystemChatMessage(string roomId, string content)
    {
        var message = _chatService.AddSystemMessage(roomId, content);
        var ev = new ChatMessageEvent
        {
            SequenceNumber = message.SequenceNumber,
            SenderName = message.SenderName,
            IsPlayer = message.IsPlayer,
            Content = message.Content,
            SentAt = message.SentAt,
            IsSystem = message.IsSystem
        };
        Dispatch(roomId, "ChatMessageReceived", () => Room(roomId).ChatMessageReceived(ev));
    }

    private void DispatchChatStatusIfChanged(string roomId)
    {
        var enabled = _chatService.IsChatEnabled(roomId);
        var previousExists = _lastChatStatus.TryGetValue(roomId, out var previous);

        if (previousExists && previous == enabled)
            return;

        _lastChatStatus[roomId] = enabled;
        var ev = new ChatStatusChangedEvent { IsChatEnabled = enabled };
        Dispatch(roomId, "ChatStatusChanged", () => Room(roomId).ChatStatusChanged(ev));
    }

    internal static IReadOnlyList<DealRecapResponse> MapToCompletedDeals(MatchState matchState)
    {
        return matchState.CompletedDeals.Select(r => new DealRecapResponse
        {
            GameMode = r.GameMode,
            Multiplier = r.Multiplier,
            AnnouncerTeam = r.AnnouncerTeam,
            Team1MatchPoints = r.Team1MatchPoints,
            Team2MatchPoints = r.Team2MatchPoints,
            WasSweep = r.WasSweep,
            SweepingTeam = r.SweepingTeam,
            IsInstantWin = r.IsInstantWin
        }).ToList();
    }

    private static TrickResponse MapToTrickResponse(TrickState trick, GameMode gameMode, PlayerPosition winner)
    {
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
            Winner = winner,
            WinningPlayer = winner
        };
    }
}
