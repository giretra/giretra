using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a player joins a room.
/// </summary>
public sealed class PlayerJoinedEvent
{
    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Display name of the player who joined.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The position the player took.
    /// </summary>
    public required PlayerPosition Position { get; init; }
}
