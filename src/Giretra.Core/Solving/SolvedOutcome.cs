using Giretra.Core.Players;

namespace Giretra.Core.Solving;

/// <summary>
/// The end-of-hand result reached when all four players play perfectly with every hand visible.
/// Card points include those already captured before the solved position and the last trick bonus.
/// </summary>
public readonly record struct SolvedOutcome(int Team1CardPoints, int Team2CardPoints, Team? SweepingTeam)
{
    /// <summary>
    /// Gets the final card points for a specific team.
    /// </summary>
    public int GetCardPoints(Team team)
        => team == Team.Team1 ? Team1CardPoints : Team2CardPoints;

    /// <summary>
    /// Gets the single number the solver optimises, from the given team's perspective:
    /// its final card points, plus <see cref="DoubleDummySolver.SweepBonus"/> if it sweeps,
    /// minus <see cref="DoubleDummySolver.SweepBonus"/> if it is swept.
    /// Match points never decrease when this score increases, in every game mode.
    /// </summary>
    public int GetScore(Team team)
    {
        var score = GetCardPoints(team);
        if (SweepingTeam == team) score += DoubleDummySolver.SweepBonus;
        else if (SweepingTeam.HasValue) score -= DoubleDummySolver.SweepBonus;
        return score;
    }
}
