using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a match ends.
/// </summary>
public sealed class MatchEndedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The winning team.
    /// </summary>
    public required Team Winner { get; init; }

    /// <summary>
    /// Team 1's final match points.
    /// </summary>
    public required int Team1MatchPoints { get; init; }

    /// <summary>
    /// Team 2's final match points.
    /// </summary>
    public required int Team2MatchPoints { get; init; }

    /// <summary>
    /// Number of deals played.
    /// </summary>
    public required int TotalDeals { get; init; }
}
