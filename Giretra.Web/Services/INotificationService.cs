using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Domain;

namespace Giretra.Web.Services;

/// <summary>
/// Service for sending real-time notifications to connected clients.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notifies a player that it's their turn.
    /// </summary>
    Task NotifyYourTurnAsync(string gameId, string clientId, PlayerPosition position, PendingActionType actionType);

    /// <summary>
    /// Notifies all clients that a deal has started.
    /// </summary>
    Task NotifyDealStartedAsync(string gameId, MatchState matchState);

    /// <summary>
    /// Notifies all clients that a deal has ended.
    /// </summary>
    Task NotifyDealEndedAsync(string gameId, DealResult result, MatchState matchState);

    /// <summary>
    /// Notifies all clients that a card was played.
    /// </summary>
    Task NotifyCardPlayedAsync(string gameId, PlayerPosition player, Card card, HandState handState, MatchState matchState);

    /// <summary>
    /// Notifies all clients that a trick was completed.
    /// </summary>
    Task NotifyTrickCompletedAsync(string gameId, TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState);

    /// <summary>
    /// Notifies all clients that the match has ended.
    /// </summary>
    Task NotifyMatchEndedAsync(string gameId, MatchState matchState);

    /// <summary>
    /// Notifies all clients in a room that a player joined.
    /// </summary>
    Task NotifyPlayerJoinedAsync(string roomId, string playerName, PlayerPosition position);

    /// <summary>
    /// Notifies all clients in a room that a player left.
    /// </summary>
    Task NotifyPlayerLeftAsync(string roomId, string playerName, PlayerPosition position);

    /// <summary>
    /// Notifies all clients in a room that the game started.
    /// </summary>
    Task NotifyGameStartedAsync(string roomId, string gameId);
}
