using System.Collections.Immutable;
using Giretra.Core.GameModes;

namespace Giretra.Manage.Validation;

/// <summary>
/// The type of decision a player agent was asked to make.
/// </summary>
public enum DecisionType
{
    Cut,
    Negotiation,
    CardPlay,
    Notification
}

/// <summary>
/// A single rule violation detected during validation.
/// </summary>
public sealed record Violation(
    DecisionType Type,
    string Description,
    int MatchNumber,
    int DealNumber);

/// <summary>
/// A single decision timing measurement.
/// </summary>
public sealed record DecisionTiming(
    DecisionType Type,
    TimeSpan Duration,
    int MatchNumber);

/// <summary>
/// A warning from a notification method that threw an exception.
/// </summary>
public sealed record NotificationWarning(
    string MethodName,
    string ExceptionMessage,
    int MatchNumber,
    int DealNumber);

/// <summary>
/// A determinism mismatch between two identical-seed runs.
/// </summary>
public sealed record DeterminismViolation(
    DecisionType Type,
    int DecisionIndex,
    string Run1Value,
    string Run2Value);

/// <summary>
/// Aggregated timing statistics for a single decision type.
/// </summary>
public sealed class DecisionTimingStats
{
    public required DecisionType Type { get; init; }
    public required int Count { get; init; }
    public required TimeSpan Min { get; init; }
    public required TimeSpan Max { get; init; }
    public required TimeSpan Average { get; init; }
    public required TimeSpan P50 { get; init; }
    public required TimeSpan P95 { get; init; }
    public required TimeSpan P99 { get; init; }

    /// <summary>
    /// Computes timing stats from a list of durations.
    /// </summary>
    public static DecisionTimingStats Compute(DecisionType type, IReadOnlyList<TimeSpan> durations)
    {
        if (durations.Count == 0)
        {
            return new DecisionTimingStats
            {
                Type = type,
                Count = 0,
                Min = TimeSpan.Zero,
                Max = TimeSpan.Zero,
                Average = TimeSpan.Zero,
                P50 = TimeSpan.Zero,
                P95 = TimeSpan.Zero,
                P99 = TimeSpan.Zero
            };
        }

        var sorted = durations.OrderBy(d => d).ToList();
        var totalTicks = sorted.Sum(d => d.Ticks);

        return new DecisionTimingStats
        {
            Type = type,
            Count = sorted.Count,
            Min = sorted[0],
            Max = sorted[^1],
            Average = TimeSpan.FromTicks(totalTicks / sorted.Count),
            P50 = Percentile(sorted, 50),
            P95 = Percentile(sorted, 95),
            P99 = Percentile(sorted, 99)
        };
    }

    private static TimeSpan Percentile(List<TimeSpan> sorted, int percentile)
    {
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}

/// <summary>
/// Performance trend comparison between first-half and second-half of matches.
/// </summary>
public sealed record PerformanceTrendEntry(
    DecisionType Type,
    TimeSpan FirstHalfMedian,
    TimeSpan SecondHalfMedian,
    bool HasDegradation);

/// <summary>
/// Configuration for a validation run.
/// </summary>
public sealed class ValidationConfig
{
    public int MatchCount { get; init; } = 100;
    public int TargetScore { get; init; } = 151;
    public int? Seed { get; init; }
    public bool Shuffle { get; init; } = true;
    public int? TimeoutMs { get; init; }
    public bool Determinism { get; init; }
    public bool Verbose { get; init; }
}

/// <summary>
/// Progress update for a single completed match during validation.
/// </summary>
public sealed class ValidationMatchProgress
{
    public required int MatchNumber { get; init; }
    public required int ViolationCount { get; init; }
    public required int CumulativeViolations { get; init; }
    public required bool Crashed { get; init; }
    public required int DealsPlayed { get; init; }
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Aggregated result of a complete validation run.
/// </summary>
public sealed class ValidationResult
{
    public required int MatchesCompleted { get; init; }
    public required int MatchesCrashed { get; init; }
    public required int TotalDeals { get; init; }
    public required ImmutableList<Violation> Violations { get; init; }
    public required ImmutableList<DecisionTimingStats> TimingStats { get; init; }
    public required ImmutableHashSet<GameMode> GameModesCovered { get; init; }
    public required int AgentTeamWins { get; init; }
    public required int OpponentTeamWins { get; init; }
    public required ImmutableList<string> CrashDetails { get; init; }
    public required TimeSpan TotalDuration { get; init; }
    public required ImmutableList<NotificationWarning> NotificationWarnings { get; init; }
    public required ImmutableList<DeterminismViolation> DeterminismViolations { get; init; }
    public required ImmutableList<PerformanceTrendEntry> PerformanceTrend { get; init; }

    public bool HasViolations =>
        Violations.Count > 0 || MatchesCrashed > 0 || DeterminismViolations.Count > 0;
}
