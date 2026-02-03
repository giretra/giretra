using Giretra.Web.Domain;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a game room.
/// </summary>
public sealed class RoomResponse
{
    /// <summary>
    /// Room identifier.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Display name of the room.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current status of the room.
    /// </summary>
    public required RoomStatus Status { get; init; }

    /// <summary>
    /// Number of human players in the room.
    /// </summary>
    public required int PlayerCount { get; init; }

    /// <summary>
    /// Number of watchers in the room.
    /// </summary>
    public required int WatcherCount { get; init; }

    /// <summary>
    /// Player slots with their current state.
    /// </summary>
    public required IReadOnlyList<PlayerSlotResponse> PlayerSlots { get; init; }

    /// <summary>
    /// Game session ID if a game is in progress.
    /// </summary>
    public string? GameId { get; init; }

    /// <summary>
    /// When the room was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
