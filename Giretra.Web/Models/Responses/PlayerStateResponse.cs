using Giretra.Core.Players;
using Giretra.Web.Domain;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for player-specific game state.
/// </summary>
public sealed class PlayerStateResponse
{
    /// <summary>
    /// The player's position.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// The player's current hand.
    /// </summary>
    public required IReadOnlyList<CardResponse> Hand { get; init; }

    /// <summary>
    /// Whether it's this player's turn.
    /// </summary>
    public required bool IsYourTurn { get; init; }

    /// <summary>
    /// The type of action being awaited (if it's your turn).
    /// </summary>
    public PendingActionType? PendingActionType { get; init; }

    /// <summary>
    /// Valid cards the player can play (if it's their turn to play).
    /// </summary>
    public IReadOnlyList<CardResponse>? ValidCards { get; init; }

    /// <summary>
    /// Valid negotiation actions (if it's their turn to negotiate).
    /// </summary>
    public IReadOnlyList<ValidActionResponse>? ValidActions { get; init; }

    /// <summary>
    /// The full game state.
    /// </summary>
    public required GameStateResponse GameState { get; init; }
}
