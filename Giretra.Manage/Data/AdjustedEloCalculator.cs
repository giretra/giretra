using Giretra.Core.Players;

namespace Giretra.Manage.Data;

/// <summary>
/// Computes adjusted ELO ratings normalized so RandomPlayer = 1000.
/// </summary>
public static class AdjustedEloCalculator
{
    /// <summary>
    /// Computes adjusted ratings from Swiss tournament participants.
    /// Returns null if RandomPlayer is not among the participants.
    /// </summary>
    public static IReadOnlyList<(IPlayerAgentFactory Factory, int AdjustedRating)>? FromSwiss(
        IReadOnlyList<Swiss.SwissParticipant> rankedParticipants)
    {
        var randomPlayer = rankedParticipants
            .FirstOrDefault(p => p.Name == "RandomPlayer");

        if (randomPlayer is null)
            return null;

        var offset = 1000.0 - randomPlayer.Elo;

        return rankedParticipants
            .Select(p => (p.Factory, AdjustedRating: (int)Math.Round(p.Elo + offset)))
            .ToList();
    }

    /// <summary>
    /// Computes adjusted ratings from a head-to-head benchmark result.
    /// Returns null if neither team is RandomPlayer.
    /// </summary>
    public static IReadOnlyList<(IPlayerAgentFactory Factory, int AdjustedRating)>? FromBenchmark(
        IPlayerAgentFactory team1Factory,
        IPlayerAgentFactory team2Factory,
        double team1FinalElo,
        double team2FinalElo)
    {
        double? randomElo = null;
        if (team1Factory.AgentName == "RandomPlayer")
            randomElo = team1FinalElo;
        else if (team2Factory.AgentName == "RandomPlayer")
            randomElo = team2FinalElo;

        if (randomElo is null)
            return null;

        var offset = 1000.0 - randomElo.Value;

        return new List<(IPlayerAgentFactory, int)>
        {
            (team1Factory, (int)Math.Round(team1FinalElo + offset)),
            (team2Factory, (int)Math.Round(team2FinalElo + offset))
        };
    }
}
