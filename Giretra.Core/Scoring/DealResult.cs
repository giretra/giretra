using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Core.Scoring;

/// <summary>
/// Represents the result of a completed deal.
/// </summary>
public sealed record DealResult
{
    /// <summary>
    /// Gets the game mode that was played.
    /// </summary>
    public GameMode GameMode { get; init; }

    /// <summary>
    /// Gets the multiplier state (Normal, Doubled, or Redoubled).
    /// </summary>
    public MultiplierState Multiplier { get; init; }

    /// <summary>
    /// Gets the team that announced (made the winning bid).
    /// </summary>
    public Team AnnouncerTeam { get; init; }

    /// <summary>
    /// Gets the card points earned by Team1.
    /// </summary>
    public int Team1CardPoints { get; init; }

    /// <summary>
    /// Gets the card points earned by Team2.
    /// </summary>
    public int Team2CardPoints { get; init; }

    /// <summary>
    /// Gets the match points awarded to Team1.
    /// </summary>
    public int Team1MatchPoints { get; init; }

    /// <summary>
    /// Gets the match points awarded to Team2.
    /// </summary>
    public int Team2MatchPoints { get; init; }

    /// <summary>
    /// Gets whether a sweep occurred (one team won all 8 tricks).
    /// </summary>
    public bool WasSweep { get; init; }

    /// <summary>
    /// Gets the team that achieved the sweep, if any.
    /// </summary>
    public Team? SweepingTeam { get; init; }

    /// <summary>
    /// Gets whether this deal results in an instant match win (Colour sweep).
    /// </summary>
    public bool IsInstantWin { get; init; }

    /// <summary>
    /// Gets whether the announcer team won this deal.
    /// </summary>
    public bool AnnouncerWon
    {
        get
        {
            var announcerPoints = AnnouncerTeam == Team.Team1 ? Team1MatchPoints : Team2MatchPoints;
            var defenderPoints = AnnouncerTeam == Team.Team1 ? Team2MatchPoints : Team1MatchPoints;
            return announcerPoints > defenderPoints || IsInstantWin;
        }
    }

    /// <summary>
    /// Gets the match points for a specific team.
    /// </summary>
    public int GetMatchPoints(Team team)
        => team == Team.Team1 ? Team1MatchPoints : Team2MatchPoints;

    /// <summary>
    /// Gets the card points for a specific team.
    /// </summary>
    public int GetCardPoints(Team team)
        => team == Team.Team1 ? Team1CardPoints : Team2CardPoints;
}
