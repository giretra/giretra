using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;

namespace Giretra.Web.Services.Offline;

/// <summary>
/// Offline mode has no persisted matches: the scoreboard is assembled from the same
/// deterministic demo deals <see cref="OfflineDealReplayService"/> serves, with a generated
/// negotiation, so the history UI can be exercised locally.
/// </summary>
public sealed class OfflineMatchSummaryService : IMatchSummaryService
{
    private readonly IDealReplayService _replays;

    public OfflineMatchSummaryService(IDealReplayService replays)
    {
        _replays = replays;
    }

    public async Task<MatchSummaryResult> GetSummaryAsync(Guid matchId, Guid userId)
    {
        var deals = new List<MatchSummaryDealResponse>();
        IReadOnlyList<DealReplayPlayerResponse> players = [];
        var t1 = 0;
        var t2 = 0;

        for (var number = 1; ; number++)
        {
            var result = await _replays.GetReplayAsync(matchId, number, userId);
            if (result.Status != DealReplayStatus.Ok)
                break;
            var replay = result.Replay!;
            players = replay.Players;

            var random = new Random(HashCode.Combine(matchId, number, 7));
            var announcerTeam = replay.AnnouncerTeam ?? Team.Team1;
            var announcer = announcerTeam == Team.Team1
                ? (random.Next(2) == 0 ? PlayerPosition.Bottom : PlayerPosition.Top)
                : (random.Next(2) == 0 ? PlayerPosition.Left : PlayerPosition.Right);
            var negotiation = new List<NegotiationActionResponse>
            {
                new() { ActionType = "Announce", Player = announcer, Mode = replay.GameMode },
            };
            var next = announcer;
            for (var i = 0; i < 3; i++)
            {
                next = next.Next();
                negotiation.Add(new NegotiationActionResponse { ActionType = "Accept", Player = next, Mode = replay.GameMode });
            }

            t1 += replay.Team1MatchPointsEarned;
            t2 += replay.Team2MatchPointsEarned;
            var winners = replay.Tricks.Select(t => t.Winner).ToList();
            var announcerPoints = announcerTeam == Team.Team1 ? replay.Team1CardPoints : replay.Team2CardPoints;
            var otherPoints = announcerTeam == Team.Team1 ? replay.Team2CardPoints : replay.Team1CardPoints;

            deals.Add(new MatchSummaryDealResponse
            {
                DealNumber = number,
                Dealer = replay.Dealer,
                GameMode = replay.GameMode,
                Multiplier = replay.Multiplier,
                AnnouncerTeam = announcerTeam,
                Announcer = announcer,
                AnnouncerWon = announcerPoints > otherPoints,
                Team1CardPoints = replay.Team1CardPoints,
                Team2CardPoints = replay.Team2CardPoints,
                Team1MatchPoints = replay.Team1MatchPointsEarned,
                Team2MatchPoints = replay.Team2MatchPointsEarned,
                Team1ScoreAfter = t1,
                Team2ScoreAfter = t2,
                WasSweep = replay.WasSweep,
                SweepingTeam = replay.SweepingTeam,
                IsInstantWin = replay.IsInstantWin,
                Negotiation = negotiation,
                TrickWinners = winners,
                Team1Tricks = winners.Count(w => w.GetTeam() == Team.Team1),
                Team2Tricks = winners.Count(w => w.GetTeam() == Team.Team2),
                IsReplayable = true,
            });
        }

        if (deals.Count == 0)
            return MatchSummaryResult.Of(MatchSummaryStatus.MatchNotFound);

        var winner = t1 > t2 ? Team.Team1 : Team.Team2;
        return MatchSummaryResult.Success(new MatchSummaryResponse
        {
            MatchId = matchId,
            IsRanked = false,
            WasAbandoned = false,
            TargetScore = 150,
            TotalDeals = deals.Count,
            WinnerTeam = winner,
            Team1FinalScore = t1,
            Team2FinalScore = t2,
            DurationSeconds = 25 * 60,
            PlayedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ViewerPosition = PlayerPosition.Bottom,
            Players = players.Select(p =>
            {
                var (tricks, contracts, made) = MatchSummaryStats.PlayerTotals(deals, p.Position);
                return new MatchSummaryPlayerResponse
                {
                    Position = p.Position,
                    Team = p.Team,
                    DisplayName = p.DisplayName,
                    IsBot = p.IsBot,
                    TricksWon = tricks,
                    Contracts = contracts,
                    ContractsMade = made,
                };
            }).ToList(),
            Deals = deals,
            Stats = MatchSummaryStats.Compute(deals, winner),
        });
    }
}
