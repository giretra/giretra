using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a player leaves a room.
/// </summary>
public sealed class PlayerLeftEvent
{
    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Display name of the player who left.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The position the player vacated.
    /// </summary>
    public required PlayerPosition Position { get; init; }
}
