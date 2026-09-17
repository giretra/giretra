using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;

namespace Giretra.Core.Solving;

/// <summary>
/// The negotiation outcome needed to convert solved card points into match points.
/// </summary>
/// <param name="AnnouncerTeam">The team whose bid is being played.</param>
/// <param name="Multiplier">The multiplier state of the deal.</param>
/// <param name="ColourSweepMatchPoints">
/// When set, a Colour sweep is worth this many base match points (× multiplier), mirroring
/// <see cref="State.MatchState.ColourSweepMatchPoints"/>. Null = normal rules (instant match win).
/// </param>
/// <param name="InstantWinMatchPoints">
/// The match point value given to an instant match win when comparing outcomes (default: the 150 match target).
/// </param>
public sealed record SolverScoring(
    Team AnnouncerTeam,
    MultiplierState Multiplier = MultiplierState.Normal,
    int? ColourSweepMatchPoints = null,
    int InstantWinMatchPoints = 150)
{
    private static readonly ScoringCalculator Calculator = new();

    /// <summary>
    /// Scores a solved outcome with the regular scoring rules.
    /// </summary>
    public DealResult Score(GameMode gameMode, SolvedOutcome outcome)
        => Calculator.Calculate(
            gameMode,
            Multiplier,
            AnnouncerTeam,
            outcome.Team1CardPoints,
            outcome.Team2CardPoints,
            outcome.SweepingTeam);

    /// <summary>
    /// Gets the match point swing of a solved outcome from a team's perspective:
    /// the match points it scores minus the match points its opponents score.
    /// </summary>
    public int GetMatchPointSwing(GameMode gameMode, SolvedOutcome outcome, Team team)
    {
        var result = Score(gameMode, outcome);

        if (result.IsInstantWin)
        {
            var sweepValue = ColourSweepMatchPoints.HasValue
                ? ColourSweepMatchPoints.Value * Multiplier.GetMultiplier()
                : InstantWinMatchPoints;
            return result.SweepingTeam == team ? sweepValue : -sweepValue;
        }

        return result.GetMatchPoints(team) - result.GetMatchPoints(team.Opponent());
    }
}
