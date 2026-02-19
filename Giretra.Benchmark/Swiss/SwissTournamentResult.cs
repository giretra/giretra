using System.Collections.Immutable;

namespace Giretra.Benchmark.Swiss;

/// <summary>
/// Final result of a Swiss tournament.
/// </summary>
public sealed class SwissTournamentResult
{
    public required ImmutableList<SwissParticipant> RankedParticipants { get; init; }
    public required ImmutableList<SwissRoundResult> Rounds { get; init; }
    public required TimeSpan TotalDuration { get; init; }

    public int TotalRounds => Rounds.Count;
    public int TotalMatches => Rounds.Sum(r => r.Matches.Count);
}
