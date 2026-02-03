using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Hubs;
using Giretra.Web.Models.Events;
using Giretra.Web.Models.Responses;
using Giretra.Web.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace Giretra.Web.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IGameRepository _gameRepository;

    public NotificationService(IHubContext<GameHub> hubContext, IGameRepository gameRepository)
    {
        _hubContext = hubContext;
        _gameRepository = gameRepository;
    }

    public async Task NotifyYourTurnAsync(string gameId, string clientId, PlayerPosition position, PendingActionType actionType)
    {
        var ev = new YourTurnEvent
        {
            GameId = gameId,
            Position = position,
            ActionType = actionType
        };

        // Send to the specific client
        await _hubContext.Clients.Group($"client_{clientId}").SendAsync("YourTurn", ev);

        // Also broadcast to room that it's this player's turn
        var session = _gameRepository.GetById(gameId);
        if (session != null)
        {
            await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("PlayerTurn", new { GameId = gameId, Position = position, ActionType = actionType });
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
    }

    public async Task NotifyDealEndedAsync(string gameId, DealResult result, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

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
            SweepingTeam = result.SweepingTeam
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("DealEnded", ev);
    }

    public async Task NotifyCardPlayedAsync(string gameId, PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        var session = _gameRepository.GetById(gameId);
        if (session == null) return;

        var ev = new CardPlayedEvent
        {
            GameId = gameId,
            Player = player,
            Card = CardResponse.FromCard(card)
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("CardPlayed", ev);
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

        var ev = new MatchEndedEvent
        {
            GameId = gameId,
            Winner = matchState.Winner!.Value,
            Team1MatchPoints = matchState.Team1MatchPoints,
            Team2MatchPoints = matchState.Team2MatchPoints,
            TotalDeals = matchState.CompletedDeals.Count
        };

        await _hubContext.Clients.Group($"room_{session.RoomId}").SendAsync("MatchEnded", ev);
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
            Winner = winner
        };
    }
}
