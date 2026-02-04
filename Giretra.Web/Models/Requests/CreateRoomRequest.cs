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
    /// Display name for the creator.
    /// </summary>
    public required string CreatorName { get; init; }

    /// <summary>
    /// Positions to fill with AI players. Only Left, Top, and Right are valid (Bottom is reserved for creator).
    /// </summary>
    public List<PlayerPosition>? AiPositions { get; init; }
}
