using Giretra.Core.GameModes;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a valid negotiation action a player can take.
/// </summary>
public sealed class ValidActionResponse
{
    /// <summary>
    /// The type of action.
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// The game mode (for Announce/Double/Redouble actions).
    /// </summary>
    public GameMode? Mode { get; init; }
}
