namespace Giretra.Benchmark.Benchmarking;

/// <summary>
/// Configuration for benchmark runs.
/// </summary>
public sealed class BenchmarkConfig
{
    /// <summary>
    /// Number of matches to play.
    /// </summary>
    public int MatchCount { get; init; } = 1000;

    /// <summary>
    /// Initial ELO rating for Team 1.
    /// </summary>
    public double Team1InitialElo { get; init; } = 1200;

    /// <summary>
    /// Initial ELO rating for Team 2.
    /// </summary>
    public double Team2InitialElo { get; init; } = 1200;

    /// <summary>
    /// K-factor for ELO calculations.
    /// </summary>
    public double EloKFactor { get; init; } = 24;

    /// <summary>
    /// Target score to win a match (default 150 for normal play, 500 for benchmarks).
    /// </summary>
    public int TargetScore { get; init; } = 500;

    /// <summary>
    /// Random seed for reproducibility. If null, uses non-deterministic randomness.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Whether to shuffle the deck at the start of each match.
    /// Required for valid statistics with deterministic agents.
    /// </summary>
    public bool Shuffle { get; init; } = false;
}
