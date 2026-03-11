using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Lightweight summary of a completed deal, used in match end recap.
/// </summary>
public sealed class DealRecapResponse
{
    /// <summary>
    /// The game mode that was played.
    /// </summary>
    public required GameMode GameMode { get; init; }

    /// <summary>
    /// The multiplier state (Normal, Doubled, Redoubled, ReRedoubled).
    /// </summary>
    public required MultiplierState Multiplier { get; init; }

    /// <summary>
    /// The team that announced (won the bid).
    /// </summary>
    public required Team AnnouncerTeam { get; init; }

    /// <summary>
    /// Team 1's match points earned in this deal.
    /// </summary>
    public required int Team1MatchPoints { get; init; }

    /// <summary>
    /// Team 2's match points earned in this deal.
    /// </summary>
    public required int Team2MatchPoints { get; init; }

    /// <summary>
    /// Whether a sweep occurred (one team won all 8 tricks).
    /// </summary>
    public required bool WasSweep { get; init; }

    /// <summary>
    /// The sweeping team if a sweep occurred.
    /// </summary>
    public Team? SweepingTeam { get; init; }

    /// <summary>
    /// Whether this deal resulted in an instant match win (Colour sweep).
    /// </summary>
    public required bool IsInstantWin { get; init; }
}
