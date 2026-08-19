namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a room is closed due to inactivity.
/// </summary>
public sealed class RoomIdleClosedEvent
{
    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }
}
