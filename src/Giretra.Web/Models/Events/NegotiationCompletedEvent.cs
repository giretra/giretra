using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when negotiation completes, before the trick-playing phase begins.
/// </summary>
public sealed class NegotiationCompletedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The resolved game mode after negotiation.
    /// </summary>
    public required GameMode ResolvedMode { get; init; }

    /// <summary>
    /// The team that announced the winning bid.
    /// </summary>
    public required Team AnnouncerTeam { get; init; }

    /// <summary>
    /// The multiplier state (Normal, Doubled, or Redoubled).
    /// </summary>
    public required MultiplierState Multiplier { get; init; }
}
