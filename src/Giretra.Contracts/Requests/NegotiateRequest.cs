using Giretra.Core.GameModes;

namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to submit a negotiation action.
/// </summary>
public sealed class NegotiateRequest
{
    /// <summary>
    /// Client ID of the player making the action.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The type of action (Announce, Accept, Double, Redouble).
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// The game mode (required for Announce, Double, Redouble).
    /// </summary>
    public GameMode? Mode { get; init; }
}
