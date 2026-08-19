namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a game starts.
/// </summary>
public sealed class GameStartedEvent
{
    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }
}
