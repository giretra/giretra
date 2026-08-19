using Giretra.Core.Players;
using Giretra.Web.Domain;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event broadcast to a room when it's a player's turn.
/// </summary>
public sealed class PlayerTurnEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The player position.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// The type of action required.
    /// </summary>
    public required PendingActionType ActionType { get; init; }

    /// <summary>
    /// When this action will time out.
    /// </summary>
    public required DateTime TimeoutAt { get; init; }
}
