namespace Giretra.Benchmark.Swiss;

/// <summary>
/// Configuration for a Swiss tournament.
/// </summary>
public sealed class SwissConfig
{
    /// <summary>
    /// Number of rounds to play.
    /// </summary>
    public int Rounds { get; init; } = 100;

    /// <summary>
    /// Target score to win a match.
    /// </summary>
    public int TargetScore { get; init; } = 500;

    /// <summary>
    /// Initial ELO rating for all participants.
    /// </summary>
    public double InitialElo { get; init; } = 1200;

    /// <summary>
    /// K-factor for ELO calculations.
    /// </summary>
    public double EloKFactor { get; init; } = 24;

    /// <summary>
    /// Random seed for reproducibility. If null, uses non-deterministic randomness.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Whether to shuffle the deck at the start of each match.
    /// </summary>
    public bool Shuffle { get; init; } = true;
}
