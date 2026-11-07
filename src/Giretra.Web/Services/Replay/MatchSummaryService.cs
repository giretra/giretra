using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;
using CoreCard = Giretra.Core.Cards.Card;
using CoreCardRank = Giretra.Core.Cards.CardRank;
using CoreCardSuit = Giretra.Core.Cards.CardSuit;
using CoreGameMode = Giretra.Core.GameModes.GameMode;
using CoreMultiplier = Giretra.Core.GameModes.MultiplierState;
using CorePosition = Giretra.Core.Players.PlayerPosition;
using CoreTeam = Giretra.Core.Players.Team;

namespace Giretra.Web.Services.Replay;

public sealed class MatchSummaryService : IMatchSummaryService
{
    private readonly GiretraDbContext _db;
    private readonly ILogger<MatchSummaryService> _logger;

    public MatchSummaryService(GiretraDbContext db, ILogger<MatchSummaryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MatchSummaryResult> GetSummaryAsync(Guid matchId, Guid userId)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player).ThenInclude(p => p.User)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player).ThenInclude(p => p.Bot)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null)
            return MatchSummaryResult.Of(MatchSummaryStatus.MatchNotFound);

        var viewer = match.MatchPlayers.FirstOrDefault(mp => mp.Player.UserId == userId);
        if (viewer == null)
            return MatchSummaryResult.Of(MatchSummaryStatus.Forbidden);

        if (match.CompletedAt == null)
            return MatchSummaryResult.Of(MatchSummaryStatus.NotCompleted);

        var dealRows = await _db.Deals
            .AsNoTracking()
            .Include(d => d.DealActions)
            .Where(d => d.MatchId == matchId)
            .OrderBy(d => d.DealNumber)
            .ToListAsync();

        var deals = new List<MatchSummaryDealResponse>(dealRows.Count);
        var t1 = 0;
        var t2 = 0;
        foreach (var deal in dealRows)
        {
            t1 += deal.Team1MatchPoints ?? 0;
            t2 += deal.Team2MatchPoints ?? 0;
            deals.Add(Build(deal, t1, t2, matchId, replayable: !match.WasAbandoned));
        }

        var winner = match.WinnerTeam.HasValue ? (CoreTeam)(int)match.WinnerTeam.Value : (CoreTeam?)null;

        var players = match.MatchPlayers
            .OrderBy(mp => mp.Position)
            .Select(mp =>
            {
                var position = (CorePosition)(int)mp.Position;
                var isBot = mp.Player.PlayerType == PlayerType.Bot;
                var (tricks, contracts, made) = MatchSummaryStats.PlayerTotals(deals, position);
                return new MatchSummaryPlayerResponse
                {
                    Position = position,
                    Team = (CoreTeam)(int)mp.Team,
                    DisplayName = isBot
                        ? mp.Player.Bot?.DisplayName ?? "Bot"
                        : mp.Player.User?.EffectiveDisplayName ?? "Player",
                    IsBot = isBot,
                    TricksWon = tricks,
                    Contracts = contracts,
                    ContractsMade = made,
                };
            })
            .ToList();

        return MatchSummaryResult.Success(new MatchSummaryResponse
        {
            MatchId = match.Id,
            IsRanked = match.IsRanked,
            WasAbandoned = match.WasAbandoned,
            TargetScore = match.TargetScore,
            TotalDeals = match.TotalDeals,
            WinnerTeam = winner,
            Team1FinalScore = match.Team1FinalScore,
            Team2FinalScore = match.Team2FinalScore,
            DurationSeconds = match.DurationSeconds,
            PlayedAt = match.CompletedAt.Value,
            ViewerPosition = (CorePosition)(int)viewer.Position,
            Players = players,
            Deals = deals,
            Stats = MatchSummaryStats.Compute(deals, winner),
        });
    }

    private MatchSummaryDealResponse Build(Deal deal, int team1After, int team2After, Guid matchId, bool replayable)
    {
        var actions = deal.DealActions.OrderBy(a => a.ActionOrder).ToList();

        var negotiation = actions
            .Where(a => a.ActionType is ActionType.Announce or ActionType.Accept
                or ActionType.Double or ActionType.Redouble or ActionType.ReRedouble)
            .Select(a => new NegotiationActionResponse
            {
                ActionType = a.ActionType.ToString(),
                Player = (CorePosition)(int)a.PlayerPosition,
                Mode = a.GameMode.HasValue ? (CoreGameMode)(int)a.GameMode.Value : null,
            })
            .ToList();

        IReadOnlyList<CorePosition> winners = [];
        if (deal.GameMode != null)
        {
            var plays = actions
                .Where(a => a.ActionType == ActionType.PlayCard && a.CardRank != null && a.CardSuit != null)
                .Select(a => new PlayedCard(
                    (CorePosition)(int)a.PlayerPosition,
                    new CoreCard((CoreCardRank)(int)a.CardRank!.Value, (CoreCardSuit)(int)a.CardSuit!.Value)))
                .ToList();

            if (plays.Count == DealReplayAssembler.PlaysPerDeal)
            {
                try
                {
                    winners = DealReplayAssembler.Assemble((CoreGameMode)(int)deal.GameMode.Value, plays)
                        .Tricks.Select(t => t.Winner).ToList();
                }
                catch (InvalidDataException ex)
                {
                    _logger.LogWarning(ex, "Deal {DealNumber} of match {MatchId} has an inconsistent play record", deal.DealNumber, matchId);
                }
            }
        }

        return new MatchSummaryDealResponse
        {
            DealNumber = deal.DealNumber,
            Dealer = (CorePosition)(int)deal.DealerPosition,
            GameMode = deal.GameMode.HasValue ? (CoreGameMode)(int)deal.GameMode.Value : null,
            Multiplier = (CoreMultiplier)(int)deal.Multiplier,
            AnnouncerTeam = deal.AnnouncerTeam.HasValue ? (CoreTeam)(int)deal.AnnouncerTeam.Value : null,
            Announcer = MatchSummaryStats.FinalAnnouncer(negotiation),
            AnnouncerWon = deal.AnnouncerWon,
            Team1CardPoints = deal.Team1CardPoints,
            Team2CardPoints = deal.Team2CardPoints,
            Team1MatchPoints = deal.Team1MatchPoints ?? 0,
            Team2MatchPoints = deal.Team2MatchPoints ?? 0,
            Team1ScoreAfter = team1After,
            Team2ScoreAfter = team2After,
            WasSweep = deal.WasSweep,
            SweepingTeam = deal.SweepingTeam.HasValue ? (CoreTeam)(int)deal.SweepingTeam.Value : null,
            IsInstantWin = deal.IsInstantWin,
            Negotiation = negotiation,
            TrickWinners = winners,
            Team1Tricks = winners.Count(w => w.GetTeam() == CoreTeam.Team1),
            Team2Tricks = winners.Count(w => w.GetTeam() == CoreTeam.Team2),
            IsReplayable = replayable && winners.Count == 8,
        };
    }
}
