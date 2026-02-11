using Giretra.Core.Players;

namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to create a new game room.
/// </summary>
public sealed class CreateRoomRequest
{
    /// <summary>
    /// Display name for the room. If not provided, auto-generated as {CreatorName}_#XXXXX.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Display name for the creator. Ignored when authenticated (display name comes from JWT).
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    /// Seats to fill with AI players, each specifying position and AI type.
    /// Only Left, Top, and Right are valid (Bottom is reserved for creator).
    /// </summary>
    public List<AiSeatRequest>? AiSeats { get; init; }

    /// <summary>
    /// Turn timer duration in seconds. Defaults to 120, range 10–300.
    /// </summary>
    public int? TurnTimerSeconds { get; init; }
}
