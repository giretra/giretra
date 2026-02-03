using Giretra.Core.Players;

namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to join a game room.
/// </summary>
public sealed class JoinRoomRequest
{
    /// <summary>
    /// Display name for the joining player.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional preferred position.
    /// </summary>
    public PlayerPosition? PreferredPosition { get; init; }
}
