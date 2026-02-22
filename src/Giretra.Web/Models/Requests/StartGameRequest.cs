namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to start a game.
/// </summary>
public sealed class StartGameRequest
{
    /// <summary>
    /// Client ID of the player requesting the start.
    /// </summary>
    public required string ClientId { get; init; }
}
