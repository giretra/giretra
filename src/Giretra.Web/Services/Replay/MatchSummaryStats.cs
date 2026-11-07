using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services.Replay;

/// <summary>
/// Pure derivations over the deals of a match: running scores, lead changes, swings, runs,
/// per-team and per-player totals. Shared by the database-backed and offline summaries.
/// </summary>
public static class MatchSummaryStats
{
    public static MatchSummaryStatsResponse Compute(IReadOnlyList<MatchSummaryDealResponse> deals, Team? winner)
    {
        var leadChanges = 0;
        Team? leader = null;
        var comeback = 0;

        int? biggestSwingDeal = null;
        var biggestSwing = 0;
        Team? biggestSwingTeam = null;

        Team? runTeam = null;
        var run = 0;
        Team? longestRunTeam = null;
        var longestRun = 0;

        foreach (var deal in deals)
        {
            var newLeader = deal.Team1ScoreAfter > deal.Team2ScoreAfter ? Team.Team1
                : deal.Team2ScoreAfter > deal.Team1ScoreAfter ? Team.Team2
                : leader;
            if (leader != null && newLeader != leader)
                leadChanges++;
            leader = newLeader;

            if (winner is { } w)
            {
                var deficit = w == Team.Team1
                    ? deal.Team2ScoreAfter - deal.Team1ScoreAfter
                    : deal.Team1ScoreAfter - deal.Team2ScoreAfter;
                comeback = Math.Max(comeback, deficit);
            }

            var swing = deal.Team1MatchPoints - deal.Team2MatchPoints;
            if (Math.Abs(swing) > biggestSwing)
            {
                biggestSwing = Math.Abs(swing);
                biggestSwingDeal = deal.DealNumber;
                biggestSwingTeam = swing > 0 ? Team.Team1 : Team.Team2;
            }

            Team? scorer = swing > 0 ? Team.Team1 : swing < 0 ? Team.Team2 : null;
            if (scorer != null && scorer == runTeam)
                run++;
            else
            {
                runTeam = scorer;
                run = scorer == null ? 0 : 1;
            }
            if (run > longestRun)
            {
                longestRun = run;
                longestRunTeam = runTeam;
            }
        }

        var announced = deals.Where(d => d.AnnouncerTeam != null).ToList();
        var byMode = deals
            .Where(d => d.GameMode != null)
            .GroupBy(d => d.GameMode!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return new MatchSummaryStatsResponse
        {
            LeadChanges = leadChanges,
            BiggestSwingDeal = biggestSwingDeal,
            BiggestSwingPoints = biggestSwing,
            BiggestSwingTeam = biggestSwingTeam,
            LongestRunTeam = longestRunTeam,
            LongestRunDeals = longestRun,
            ComebackFrom = comeback,
            Team1Sweeps = deals.Count(d => d.WasSweep && d.SweepingTeam == Team.Team1),
            Team2Sweeps = deals.Count(d => d.WasSweep && d.SweepingTeam == Team.Team2),
            Team1Contracts = announced.Count(d => d.AnnouncerTeam == Team.Team1),
            Team1ContractsMade = announced.Count(d => d.AnnouncerTeam == Team.Team1 && d.AnnouncerWon == true),
            Team2Contracts = announced.Count(d => d.AnnouncerTeam == Team.Team2),
            Team2ContractsMade = announced.Count(d => d.AnnouncerTeam == Team.Team2 && d.AnnouncerWon == true),
            Team1Tricks = deals.Sum(d => d.Team1Tricks),
            Team2Tricks = deals.Sum(d => d.Team2Tricks),
            DoubledDeals = deals.Count(d => d.Multiplier != MultiplierState.Normal),
            DealsByMode = byMode,
        };
    }

    /// <summary>Per-player trick and contract totals over the deals.</summary>
    public static (int TricksWon, int Contracts, int ContractsMade) PlayerTotals(
        IReadOnlyList<MatchSummaryDealResponse> deals, PlayerPosition position)
    {
        var tricks = deals.Sum(d => d.TrickWinners.Count(w => w == position));
        var contracts = deals.Count(d => d.Announcer == position);
        var made = deals.Count(d => d.Announcer == position && d.AnnouncerWon == true);
        return (tricks, contracts, made);
    }

    /// <summary>
    /// Resolves the announcer of the final contract: the player of the last Announce action.
    /// </summary>
    public static PlayerPosition? FinalAnnouncer(IReadOnlyList<NegotiationActionResponse> negotiation)
        => negotiation.LastOrDefault(a => a.ActionType == "Announce")?.Player;
}
