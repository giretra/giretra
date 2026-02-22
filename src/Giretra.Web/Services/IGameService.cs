using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

/// <summary>
/// Service for managing game sessions.
/// </summary>
public interface IGameService
{
    /// <summary>
    /// Creates and starts a new game from a room.
    /// </summary>
    GameSession? CreateGame(Room room);

    /// <summary>
    /// Gets a game session by ID.
    /// </summary>
    GameSession? GetGame(string gameId);

    /// <summary>
    /// Gets the game state response for a game.
    /// </summary>
    GameStateResponse? GetGameState(string gameId);

    /// <summary>
    /// Gets the player-specific view for a game.
    /// </summary>
    PlayerStateResponse? GetPlayerState(string gameId, string clientId);

    /// <summary>
    /// Submits a cut decision.
    /// </summary>
    bool SubmitCut(string gameId, string clientId, int position, bool fromTop);

    /// <summary>
    /// Submits a negotiation action.
    /// </summary>
    bool SubmitNegotiation(string gameId, string clientId, NegotiationAction action);

    /// <summary>
    /// Submits a card play.
    /// </summary>
    bool SubmitCardPlay(string gameId, string clientId, Card card);

    /// <summary>
    /// Submits confirmation to continue to the next deal.
    /// </summary>
    bool SubmitContinueDeal(string gameId, string clientId);

    /// <summary>
    /// Submits confirmation to continue after match ends.
    /// </summary>
    bool SubmitContinueMatch(string gameId, string clientId);

    /// <summary>
    /// Gets the watcher-specific view for a game (hides player hands).
    /// </summary>
    WatcherStateResponse? GetWatcherState(string gameId);

    /// <summary>
    /// Handles player abandonment during an active game.
    /// Cancels the game loop, persists the abandoned match with Elo penalties, and notifies the room.
    /// </summary>
    Task AbandonGameAsync(string gameId, PlayerPosition abandonerPosition);
}
