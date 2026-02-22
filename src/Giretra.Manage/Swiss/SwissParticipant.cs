using Giretra.Core.Players;

namespace Giretra.Manage.Swiss;

/// <summary>
/// Mutable state for a Swiss tournament participant.
/// </summary>
public sealed class SwissParticipant
{
    public SwissParticipant(IPlayerAgentFactory factory, double initialElo)
    {
        Factory = factory;
        Elo = initialElo;
        MinElo = initialElo;
        MaxElo = initialElo;
    }

    public IPlayerAgentFactory Factory { get; }
    public string Name => Factory.AgentName;
    public string DisplayName => Factory.DisplayName;

    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Byes { get; set; }

    public double Elo { get; set; }
    public double MinElo { get; set; }
    public double MaxElo { get; set; }

    /// <summary>
    /// Tournament points (wins + byes).
    /// </summary>
    public int Points => Wins + Byes;

    /// <summary>
    /// Total matches played (excluding byes).
    /// </summary>
    public int MatchesPlayed => Wins + Losses;

    /// <summary>
    /// Win rate as a percentage (0-100).
    /// </summary>
    public double WinRate => MatchesPlayed == 0 ? 0 : 100.0 * Wins / MatchesPlayed;

    public void UpdateElo(double newElo)
    {
        Elo = newElo;
        MinElo = Math.Min(MinElo, newElo);
        MaxElo = Math.Max(MaxElo, newElo);
    }
}
