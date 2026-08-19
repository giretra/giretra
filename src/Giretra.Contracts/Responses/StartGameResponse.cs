namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO when starting a game.
/// </summary>
public sealed class StartGameResponse
{
    /// <summary>
    /// The game session ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }
}
