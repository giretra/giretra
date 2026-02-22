using Giretra.Core.Players;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a trick is completed.
/// </summary>
public sealed class TrickCompletedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The completed trick.
    /// </summary>
    public required TrickResponse Trick { get; init; }

    /// <summary>
    /// The winner of the trick.
    /// </summary>
    public required PlayerPosition Winner { get; init; }

    /// <summary>
    /// Team 1's current card points.
    /// </summary>
    public required int Team1CardPoints { get; init; }

    /// <summary>
    /// Team 2's current card points.
    /// </summary>
    public required int Team2CardPoints { get; init; }
}
