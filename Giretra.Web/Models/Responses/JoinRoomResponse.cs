using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO when joining a room.
/// </summary>
public sealed class JoinRoomResponse
{
    /// <summary>
    /// Client ID to use for subsequent requests.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Assigned player position (null for watchers).
    /// </summary>
    public PlayerPosition? Position { get; init; }

    /// <summary>
    /// The room that was joined.
    /// </summary>
    public required RoomResponse Room { get; init; }
}
