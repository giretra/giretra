using System.Collections.Immutable;

namespace Giretra.Benchmark.Benchmarking;

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
    /// Number of wins for Team 1.
    /// </summary>
    public required int Team1Wins { get; init; }

    /// <summary>
    /// Number of wins for Team 2.
    /// </summary>
    public required int Team2Wins { get; init; }

    /// <summary>
    /// Team 1's win rate (0.0 to 1.0).
    /// </summary>
    public double Team1WinRate => Team1Wins + Team2Wins == 0 ? 0 : (double)Team1Wins / (Team1Wins + Team2Wins);

    /// <summary>
    /// Team 2's win rate (0.0 to 1.0).
    /// </summary>
    public double Team2WinRate => Team1Wins + Team2Wins == 0 ? 0 : (double)Team2Wins / (Team1Wins + Team2Wins);

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
}
