namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to leave a game room.
/// </summary>
public sealed class LeaveRoomRequest
{
    /// <summary>
    /// Client ID of the player leaving.
    /// </summary>
    public required string ClientId { get; init; }
}
