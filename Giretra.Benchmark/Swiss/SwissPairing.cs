namespace Giretra.Benchmark.Swiss;

/// <summary>
/// Swiss tournament pairing logic.
/// </summary>
public static class SwissPairing
{
    /// <summary>
    /// Pairs participants for a round using Swiss pairing rules.
    /// Sorted by points descending, then ELO descending. Adjacent players are paired.
    /// If odd count, the lowest-ranked player with fewest byes gets a bye.
    /// </summary>
    public static (List<(SwissParticipant P1, SwissParticipant P2)> Pairings, SwissParticipant? Bye) PairRound(
        IReadOnlyList<SwissParticipant> participants)
    {
        var sorted = participants
            .OrderByDescending(p => p.Points)
            .ThenByDescending(p => p.Elo)
            .ToList();

        SwissParticipant? bye = null;

        if (sorted.Count % 2 != 0)
        {
            // Give bye to the lowest-ranked player with fewest byes
            bye = sorted
                .OrderBy(p => p.Byes)
                .ThenBy(p => p.Points)
                .ThenBy(p => p.Elo)
                .First();

            sorted.Remove(bye);
        }

        var pairings = new List<(SwissParticipant, SwissParticipant)>();
        for (int i = 0; i + 1 < sorted.Count; i += 2)
        {
            pairings.Add((sorted[i], sorted[i + 1]));
        }

        return (pairings, bye);
    }
}
