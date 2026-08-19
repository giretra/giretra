namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for listing rooms.
/// </summary>
public sealed class RoomListResponse
{
    /// <summary>
    /// The list of rooms.
    /// </summary>
    public required IReadOnlyList<RoomResponse> Rooms { get; init; }

    /// <summary>
    /// Total count of rooms.
    /// </summary>
    public required int TotalCount { get; init; }
}
