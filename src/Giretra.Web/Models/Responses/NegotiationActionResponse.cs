using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a negotiation action.
/// </summary>
public sealed class NegotiationActionResponse
{
    /// <summary>
    /// The type of action.
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// The player who took the action.
    /// </summary>
    public required PlayerPosition Player { get; init; }

    /// <summary>
    /// The game mode (for Announce/Double/Redouble actions).
    /// </summary>
    public GameMode? Mode { get; init; }

    public static NegotiationActionResponse FromAction(NegotiationAction action)
    {
        return action switch
        {
            AnnouncementAction a => new NegotiationActionResponse
            {
                ActionType = "Announce",
                Player = a.Player,
                Mode = a.Mode
            },
            AcceptAction a => new NegotiationActionResponse
            {
                ActionType = "Accept",
                Player = a.Player,
                Mode = null
            },
            DoubleAction a => new NegotiationActionResponse
            {
                ActionType = "Double",
                Player = a.Player,
                Mode = a.TargetMode
            },
            RedoubleAction a => new NegotiationActionResponse
            {
                ActionType = "Redouble",
                Player = a.Player,
                Mode = a.TargetMode
            },
            _ => throw new ArgumentException($"Unknown action type: {action.GetType().Name}")
        };
    }
}
