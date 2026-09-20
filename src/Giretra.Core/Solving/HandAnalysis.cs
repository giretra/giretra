using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Core.Solving;

/// <summary>
/// One card played during a hand, measured against the solver.
/// </summary>
/// <param name="TrickNumber">The trick number (1-8).</param>
/// <param name="Player">The player who played the card.</param>
/// <param name="Card">The card played.</param>
/// <param name="Evaluation">The solved values of every card that was valid at that point.</param>
/// <param name="CardPointLoss">Card points given up compared to the best card.</param>
/// <param name="MatchPointLoss">Match points given up compared to the best card (null without scoring context).</param>
/// <param name="IsOptimal">Whether the card was one of the best cards.</param>
/// <param name="IsForced">Whether it was the only valid card.</param>
public sealed record PlayAnalysis(
    int TrickNumber,
    PlayerPosition Player,
    Card Card,
    PositionEvaluation Evaluation,
    int CardPointLoss,
    int? MatchPointLoss,
    bool IsOptimal,
    bool IsForced);

/// <summary>
/// A player's totals over an analysed hand.
/// </summary>
/// <param name="Player">The player.</param>
/// <param name="Plays">Cards played.</param>
/// <param name="Decisions">Cards played when more than one card was valid.</param>
/// <param name="Mistakes">Cards played that were not optimal.</param>
/// <param name="CardPointLoss">Total card points given up.</param>
/// <param name="MatchPointLoss">Total match points given up (null without scoring context).</param>
public sealed record PlayerAnalysisSummary(
    PlayerPosition Player,
    int Plays,
    int Decisions,
    int Mistakes,
    int CardPointLoss,
    int? MatchPointLoss);

/// <summary>
/// The solver's review of a played hand.
/// </summary>
public sealed class HandAnalysis
{
    /// <summary>
    /// Gets the game mode of the hand.
    /// </summary>
    public GameMode GameMode { get; }

    /// <summary>
    /// Gets the scoring context used for match point losses, if any.
    /// </summary>
    public SolverScoring? Scoring { get; }

    /// <summary>
    /// Gets the result of perfect play by everyone from the first card ("par" for the deal).
    /// </summary>
    public SolvedOutcome Par { get; }

    /// <summary>
    /// Gets the actual result of the hand (or, for a hand in progress, the result of perfect play from here).
    /// </summary>
    public SolvedOutcome Final { get; }

    /// <summary>
    /// Gets every card played, in play order.
    /// </summary>
    public IReadOnlyList<PlayAnalysis> Plays { get; }

    internal HandAnalysis(
        GameMode gameMode,
        SolverScoring? scoring,
        SolvedOutcome par,
        SolvedOutcome final,
        IReadOnlyList<PlayAnalysis> plays)
    {
        GameMode = gameMode;
        Scoring = scoring;
        Par = par;
        Final = final;
        Plays = plays;
    }

    /// <summary>
    /// Gets the plays that were not optimal.
    /// </summary>
    public IEnumerable<PlayAnalysis> Mistakes => Plays.Where(p => !p.IsOptimal);

    /// <summary>
    /// Gets a player's totals over the hand.
    /// </summary>
    public PlayerAnalysisSummary GetSummary(PlayerPosition player)
    {
        var plays = Plays.Where(p => p.Player == player).ToList();
        return new PlayerAnalysisSummary(
            player,
            plays.Count,
            plays.Count(p => !p.IsForced),
            plays.Count(p => !p.IsOptimal),
            plays.Sum(p => p.CardPointLoss),
            Scoring is null ? null : plays.Sum(p => p.MatchPointLoss ?? 0));
    }
}
