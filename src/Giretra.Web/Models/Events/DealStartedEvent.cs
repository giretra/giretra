using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a new deal starts.
/// </summary>
public sealed class DealStartedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The dealer for this deal.
    /// </summary>
    public required PlayerPosition Dealer { get; init; }

    /// <summary>
    /// Deal number (1-based).
    /// </summary>
    public required int DealNumber { get; init; }
}
