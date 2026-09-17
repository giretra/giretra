using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.Solving;

namespace Giretra.Manage.Analysis;

/// <summary>
/// A deal played during an analysis run, reviewed by the solver.
/// </summary>
public sealed record AnalyzedDeal(int MatchNumber, int DealNumber, DealResult Result, HandAnalysis Analysis);

/// <summary>
/// A team's card play quality over a set of deals, measured against the solver.
/// </summary>
public sealed record TeamPlayQuality
{
    public int Deals { get; init; }

    /// <summary>Cards played when more than one card was valid.</summary>
    public int Decisions { get; init; }

    /// <summary>Decisions where the card played was not one of the best cards.</summary>
    public int Mistakes { get; init; }

    public int CardPointLoss { get; init; }
    public int MatchPointLoss { get; init; }

    public double Accuracy => Decisions == 0 ? 1 : 1 - (double)Mistakes / Decisions;
    public double CardPointLossPerDeal => Deals == 0 ? 0 : (double)CardPointLoss / Deals;
    public double MatchPointLossPerDeal => Deals == 0 ? 0 : (double)MatchPointLoss / Deals;

    public static TeamPlayQuality From(IEnumerable<AnalyzedDeal> deals, Team team)
    {
        var quality = new TeamPlayQuality();
        foreach (var deal in deals)
        {
            var plays = deal.Analysis.Plays.Where(p => p.Player.GetTeam() == team).ToList();
            quality = quality with
            {
                Deals = quality.Deals + 1,
                Decisions = quality.Decisions + plays.Count(p => !p.IsForced),
                Mistakes = quality.Mistakes + plays.Count(p => !p.IsOptimal),
                CardPointLoss = quality.CardPointLoss + plays.Sum(p => p.CardPointLoss),
                MatchPointLoss = quality.MatchPointLoss + plays.Sum(p => p.MatchPointLoss ?? 0)
            };
        }

        return quality;
    }
}

/// <summary>
/// The outcome of an analysis run.
/// </summary>
public sealed class AnalysisResult
{
    public required string Team1Name { get; init; }
    public required string Team2Name { get; init; }
    public required int Matches { get; init; }
    public required int Team1Wins { get; init; }
    public required int Team2Wins { get; init; }
    public required IReadOnlyList<AnalyzedDeal> Deals { get; init; }
    public required TimeSpan PlayDuration { get; init; }
    public required TimeSpan SolverDuration { get; init; }

    public TeamPlayQuality GetQuality(Team team, GameMode? mode = null)
        => TeamPlayQuality.From(Deals.Where(d => mode is null || d.Result.GameMode == mode), team);
}
