using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent when a deal ends.
/// </summary>
public sealed class DealEndedEvent
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The game mode for the deal.
    /// </summary>
    public required GameMode GameMode { get; init; }

    /// <summary>
    /// Team 1's card points for this deal.
    /// </summary>
    public required int Team1CardPoints { get; init; }

    /// <summary>
    /// Team 2's card points for this deal.
    /// </summary>
    public required int Team2CardPoints { get; init; }

    /// <summary>
    /// Team 1's match points earned.
    /// </summary>
    public required int Team1MatchPointsEarned { get; init; }

    /// <summary>
    /// Team 2's match points earned.
    /// </summary>
    public required int Team2MatchPointsEarned { get; init; }

    /// <summary>
    /// Team 1's total match points.
    /// </summary>
    public required int Team1TotalMatchPoints { get; init; }

    /// <summary>
    /// Team 2's total match points.
    /// </summary>
    public required int Team2TotalMatchPoints { get; init; }

    /// <summary>
    /// Whether a sweep occurred.
    /// </summary>
    public required bool WasSweep { get; init; }

    /// <summary>
    /// The sweeping team if a sweep occurred.
    /// </summary>
    public Team? SweepingTeam { get; init; }

    /// <summary>
    /// Breakdown of Team 1's card points by card type.
    /// </summary>
    public required CardPointsBreakdownResponse Team1Breakdown { get; init; }

    /// <summary>
    /// Breakdown of Team 2's card points by card type.
    /// </summary>
    public required CardPointsBreakdownResponse Team2Breakdown { get; init; }
}
