namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a room is reset after a game ends.
/// </summary>
public sealed class RoomResetEvent
{
    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }
}
