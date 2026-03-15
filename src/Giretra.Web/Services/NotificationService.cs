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
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IGameRepository _gameRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IChatService _chatService;
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, bool> _lastChatStatus = new();

    public NotificationService(
        IHubContext<GameHub> hubContext,
        IGameRepository gameRepository,
        IServiceProvider serviceProvider,
        IChatService chatService,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _gameRepository = gameRepository;
        _serviceProvider = serviceProvider;
        _chatService = chatService;
        _logger = logger;
    }

    public async Task NotifyYourTurnAsync(string gameId, string clientId, PlayerPosition position, PendingActionType actionType, DateTime timeoutAt)
    {
        var ev = new YourTurnEvent
        {
            GameId = gameId,
            Position = position,
            ActionType = actionType,
            TimeoutAt = timeoutAt
        };

        // Send to the specific client
        await _hubContext.Clients.Group($"client_{clientId}").SendAsync("YourTurn", ev);

        // Also broadcast to room that it's this player's turn
        var session = _gameRepository.GetById(gameId);
        if (session != null)
        {
            await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("PlayerTurn", new { GameId = gameId, Position = position, ActionType = actionType, TimeoutAt = timeoutAt });
            await BroadcastChatStatusIfChangedAsync(session.RoomId);
        }
    }

    public async Task NotifyDealStartedAsync(string gameId, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        var ev = new DealStartedEvent
        {
            GameId = gameId,
            Dealer = matchState.CurrentDealer,
            DealNumber = matchState.CompletedDeals.Count + 1
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("DealStarted", ev);
        await BroadcastChatStatusIfChangedAsync(session.RoomId);
    }

    public async Task NotifyNegotiationCompletedAsync(string gameId, NegotiationState negotiationState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        var deal = matchState.CurrentDeal!;
        var ev = new NegotiationCompletedEvent
        {
            GameId = gameId,
            ResolvedMode = deal.ResolvedMode!.Value,
            AnnouncerTeam = deal.AnnouncerTeam!.Value,
            Multiplier = deal.Multiplier!.Value
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("NegotiationCompleted", ev);
        await BroadcastChatStatusIfChangedAsync(session.RoomId);
    }

    public async Task NotifyDealEndedAsync(string gameId, DealResult result, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

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

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("DealEnded", ev);
        await BroadcastChatStatusIfChangedAsync(session.RoomId);
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

    public async Task NotifyCardPlayedAsync(string gameId, PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        var playType = DetermineCardPlayType(player, card, handState, matchState);

        var ev = new CardPlayedEvent
        {
            GameId = gameId,
            Player = player,
            Card = CardResponse.FromCard(card),
            PlayType = playType
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("CardPlayed", ev);
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


    public async Task NotifyTrickCompletedAsync(string gameId, TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        var ev = new TrickCompletedEvent
        {
            GameId = gameId,
            Trick = MapToTrickResponse(completedTrick, handState.GameMode, winner),
            Winner = winner,
            Team1CardPoints = handState.Team1CardPoints,
            Team2CardPoints = handState.Team2CardPoints
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("TrickCompleted", ev);
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

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("MatchEnded", ev);
        await BroadcastChatStatusIfChangedAsync(session.RoomId);
    }

    public async Task NotifyPlayerJoinedAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerJoinedEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("PlayerJoined", ev);
    }

    public async Task NotifyPlayerLeftAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerLeftEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("PlayerLeft", ev);
    }

    public async Task NotifyGameStartedAsync(string roomId, string gameId)
    {
        var ev = new GameStartedEvent
        {
            RoomId = roomId,
            GameId = gameId
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("GameStarted", ev);
        await BroadcastChatStatusIfChangedAsync(roomId);
    }

    public async Task NotifyPlayerKickedAsync(string roomId, string playerName, PlayerPosition position)
    {
        var ev = new PlayerKickedEvent
        {
            RoomId = roomId,
            PlayerName = playerName,
            Position = position
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("PlayerKicked", ev);
    }

    public async Task NotifySeatModeChangedAsync(string roomId, PlayerPosition position, Domain.SeatAccessMode accessMode)
    {
        var ev = new SeatModeChangedEvent
        {
            RoomId = roomId,
            Position = position,
            AccessMode = accessMode
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("SeatModeChanged", ev);
    }

    public async Task NotifyMatchAbandonedAsync(string gameId, string roomId, PlayerPosition abandoner, Team winnerTeam)
    {
        var ev = new MatchAbandonedEvent
        {
            GameId = gameId,
            Abandoner = abandoner,
            WinnerTeam = winnerTeam
        };

        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("MatchAbandoned", ev);
    }

    public async Task NotifyRoomIdleClosedAsync(string roomId)
    {
        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("RoomIdleClosed", new { RoomId = roomId });
    }

    public async Task NotifyRoomsChangedAsync()
    {
        await _hubContext.Clients.Group("lobby").SendAsync("RoomsChanged");
    }

    public async Task NotifyPendingFriendCountChangedAsync(Guid userId, int count)
    {
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("PendingFriendCountChanged", new { Count = count });
    }

    private async Task BroadcastChatStatusIfChangedAsync(string roomId)
    {
        var enabled = _chatService.IsChatEnabled(roomId);
        var previousExists = _lastChatStatus.TryGetValue(roomId, out var previous);

        if (previousExists && previous == enabled)
            return;

        _lastChatStatus[roomId] = enabled;
        var ev = new ChatStatusChangedEvent { IsChatEnabled = enabled };
        await _hubContext.Clients.Group($"room_{roomId}").SendAsync("ChatStatusChanged", ev);
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
