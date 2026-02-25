using System.Collections.Immutable;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Manage.Stats;

namespace Giretra.Manage.Benchmarking;

/// <summary>
/// Aggregated results of a complete benchmark run.
/// </summary>
public sealed class BenchmarkResult
{
    /// <summary>
    /// Name of Team 1's agent.
    /// </summary>
    public required string Team1Name { get; init; }

    /// <summary>
    /// Name of Team 2's agent.
    /// </summary>
    public required string Team2Name { get; init; }

    /// <summary>
    /// Team 1's initial ELO rating.
    /// </summary>
    public required double Team1InitialElo { get; init; }

    /// <summary>
    /// Team 2's initial ELO rating.
    /// </summary>
    public required double Team2InitialElo { get; init; }

    /// <summary>
    /// Team 1's final ELO rating.
    /// </summary>
    public required double Team1FinalElo { get; init; }

    /// <summary>
    /// Team 2's final ELO rating.
    /// </summary>
    public required double Team2FinalElo { get; init; }

    /// <summary>
    /// Team 1's minimum ELO during the benchmark.
    /// </summary>
    public required double Team1MinElo { get; init; }

    /// <summary>
    /// Team 1's maximum ELO during the benchmark.
    /// </summary>
    public required double Team1MaxElo { get; init; }

    /// <summary>
    /// Team 2's minimum ELO during the benchmark.
    /// </summary>
    public required double Team2MinElo { get; init; }

    /// <summary>
    /// Team 2's maximum ELO during the benchmark.
    /// </summary>
    public required double Team2MaxElo { get; init; }

    /// <summary>
    /// Number of wins for Team 1.
    /// </summary>
    public required int Team1Wins { get; init; }

    /// <summary>
    /// Number of wins for Team 2.
    /// </summary>
    public required int Team2Wins { get; init; }

    /// <summary>
    /// Total number of matches played.
    /// </summary>
    public int TotalMatches => Team1Wins + Team2Wins;

    /// <summary>
    /// Team 1's win rate (0.0 to 1.0).
    /// </summary>
    public double Team1WinRate => TotalMatches == 0 ? 0 : (double)Team1Wins / TotalMatches;

    /// <summary>
    /// Team 2's win rate (0.0 to 1.0).
    /// </summary>
    public double Team2WinRate => TotalMatches == 0 ? 0 : (double)Team2Wins / TotalMatches;

    /// <summary>
    /// 95% confidence interval for Team 1's win rate.
    /// </summary>
    public (double Lower, double Upper) Team1WinRateConfidenceInterval =>
        StatisticsCalculator.WilsonConfidenceInterval(Team1Wins, TotalMatches);

    /// <summary>
    /// 95% confidence interval for Team 2's win rate.
    /// </summary>
    public (double Lower, double Upper) Team2WinRateConfidenceInterval =>
        StatisticsCalculator.WilsonConfidenceInterval(Team2Wins, TotalMatches);

    /// <summary>
    /// P-value for the hypothesis test that win rate differs from 50%.
    /// </summary>
    public double PValue => StatisticsCalculator.BinomialTestPValue(Team1Wins, TotalMatches);

    /// <summary>
    /// Whether the win rate difference is statistically significant at p &lt; 0.05.
    /// </summary>
    public bool IsSignificant => StatisticsCalculator.IsSignificant(PValue);

    /// <summary>
    /// Human-readable interpretation of statistical significance.
    /// </summary>
    public string SignificanceInterpretation => StatisticsCalculator.InterpretPValue(PValue);

    /// <summary>
    /// Total number of deals played across all matches.
    /// </summary>
    public required int TotalDeals { get; init; }

    /// <summary>
    /// Average number of deals per match.
    /// </summary>
    public double AverageDealsPerMatch => Matches.Count == 0 ? 0 : (double)TotalDeals / Matches.Count;

    /// <summary>
    /// Total duration of the benchmark.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// All individual match results.
    /// </summary>
    public required ImmutableList<MatchResult> Matches { get; init; }

    /// <summary>
    /// Gets win statistics broken down by game mode and announcing team.
    /// </summary>
    public ImmutableList<GameModeStats> GetGameModeStats()
    {
        var allDeals = Matches.SelectMany(m => m.DealResults).ToList();

        return Enum.GetValues<GameMode>()
            .Select(mode =>
            {
                var deals = allDeals.Where(d => d.GameMode == mode).ToList();
                var team1Announced = deals.Where(d => d.AnnouncerTeam == Team.Team1).ToList();
                var team2Announced = deals.Where(d => d.AnnouncerTeam == Team.Team2).ToList();
                return new GameModeStats(
                    mode,
                    deals.Count,
                    new AnnouncerStats(team1Announced.Count, team1Announced.Count(d => d.AnnouncerWon)),
                    new AnnouncerStats(team2Announced.Count, team2Announced.Count(d => d.AnnouncerWon)));
            })
            .ToImmutableList();
    }
}

/// <summary>
/// Win statistics for deals announced by a specific team.
/// </summary>
public sealed record AnnouncerStats(int Announced, int AnnouncerWins)
{
    public double AnnouncerWinRate => Announced == 0 ? 0 : (double)AnnouncerWins / Announced;
}

/// <summary>
/// Win statistics for a single game mode, broken down by announcing team.
/// </summary>
public sealed record GameModeStats(
    GameMode GameMode,
    int TotalDeals,
    AnnouncerStats Team1Announced,
    AnnouncerStats Team2Announced);
