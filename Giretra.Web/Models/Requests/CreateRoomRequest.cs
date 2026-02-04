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
    /// If true, immediately fill the other 3 seats with AI players.
    /// </summary>
    public bool FillWithAi { get; init; }
}
