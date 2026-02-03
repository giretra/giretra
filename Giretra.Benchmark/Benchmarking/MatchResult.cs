using Giretra.Core.Players;

namespace Giretra.Benchmark.Benchmarking;

/// <summary>
/// Result of a single match in the benchmark.
/// </summary>
public sealed class MatchResult
{
    /// <summary>
    /// The match number (1-based).
    /// </summary>
    public required int MatchNumber { get; init; }

    /// <summary>
    /// The winning team.
    /// </summary>
    public required Team Winner { get; init; }

    /// <summary>
    /// Team 1's final score.
    /// </summary>
    public required int Team1FinalScore { get; init; }

    /// <summary>
    /// Team 2's final score.
    /// </summary>
    public required int Team2FinalScore { get; init; }

    /// <summary>
    /// Number of deals played in this match.
    /// </summary>
    public required int DealsPlayed { get; init; }

    /// <summary>
    /// Team 1's ELO rating after this match.
    /// </summary>
    public required double Team1EloAfter { get; init; }

    /// <summary>
    /// Team 2's ELO rating after this match.
    /// </summary>
    public required double Team2EloAfter { get; init; }

    /// <summary>
    /// Team 1's ELO change from this match.
    /// </summary>
    public required double Team1EloChange { get; init; }

    /// <summary>
    /// Duration of the match.
    /// </summary>
    public required TimeSpan Duration { get; init; }
}
