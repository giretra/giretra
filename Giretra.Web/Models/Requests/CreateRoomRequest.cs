namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to create a new game room.
/// </summary>
public sealed class CreateRoomRequest
{
    /// <summary>
    /// Display name for the room.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display name for the creator.
    /// </summary>
    public required string CreatorName { get; init; }
}
