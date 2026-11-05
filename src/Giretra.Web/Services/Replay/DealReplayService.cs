using Giretra.Core.Play;
using Giretra.Model;
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

public sealed class DealReplayService : IDealReplayService
{
    private readonly GiretraDbContext _db;
    private readonly ILogger<DealReplayService> _logger;

    public DealReplayService(GiretraDbContext db, ILogger<DealReplayService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DealReplayResult> GetReplayAsync(Guid matchId, int dealNumber, Guid userId)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player).ThenInclude(p => p.User)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player).ThenInclude(p => p.Bot)
            .FirstOrDefaultAsync(m => m.Id == matchId);
        if (match == null)
            return DealReplayResult.Of(DealReplayStatus.MatchNotFound);

        if (!match.MatchPlayers.Any(mp => mp.Player.UserId == userId))
            return DealReplayResult.Of(DealReplayStatus.Forbidden);

        if (match.CompletedAt == null || match.WasAbandoned)
            return DealReplayResult.Of(DealReplayStatus.NotReplayable);

        var deal = await _db.Deals
            .AsNoTracking()
            .Include(d => d.DealActions)
            .FirstOrDefaultAsync(d => d.MatchId == matchId && d.DealNumber == dealNumber);
        if (deal == null)
            return DealReplayResult.Of(DealReplayStatus.DealNotFound);

        if (deal.GameMode == null)
            return DealReplayResult.Of(DealReplayStatus.NotReplayable);

        var plays = deal.DealActions
            .Where(a => a.ActionType == ActionType.PlayCard && a.CardRank != null && a.CardSuit != null)
            .OrderBy(a => a.ActionOrder)
            .Select(a => new PlayedCard(
                (CorePosition)(int)a.PlayerPosition,
                new CoreCard((CoreCardRank)(int)a.CardRank!.Value, (CoreCardSuit)(int)a.CardSuit!.Value)))
            .ToList();

        if (plays.Count != DealReplayAssembler.PlaysPerDeal)
            return DealReplayResult.Of(DealReplayStatus.NotReplayable);

        var gameMode = (CoreGameMode)(int)deal.GameMode.Value;
        DealReplayAssembler.Result assembled;
        try
        {
            assembled = DealReplayAssembler.Assemble(gameMode, plays);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Deal {DealNumber} of match {MatchId} has an inconsistent play record", dealNumber, matchId);
            return DealReplayResult.Of(DealReplayStatus.NotReplayable);
        }

        if (deal.Team1CardPoints is int t1 && deal.Team2CardPoints is int t2
            && (t1 != assembled.Team1CardPoints || t2 != assembled.Team2CardPoints))
        {
            _logger.LogWarning(
                "Replayed card points {T1}/{T2} differ from stored {S1}/{S2} for deal {DealNumber} of match {MatchId}",
                assembled.Team1CardPoints, assembled.Team2CardPoints, t1, t2, dealNumber, matchId);
        }

        var before = await _db.Deals
            .Where(d => d.MatchId == matchId && d.DealNumber < dealNumber)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Team1 = g.Sum(d => d.Team1MatchPoints ?? 0),
                Team2 = g.Sum(d => d.Team2MatchPoints ?? 0),
            })
            .FirstOrDefaultAsync();

        var players = match.MatchPlayers
            .OrderBy(mp => mp.Position)
            .Select(mp => new DealReplayPlayerResponse
            {
                Position = (CorePosition)(int)mp.Position,
                Team = (CoreTeam)(int)mp.Team,
                DisplayName = mp.Player.PlayerType == PlayerType.Bot
                    ? mp.Player.Bot?.DisplayName ?? "Bot"
                    : mp.Player.User?.EffectiveDisplayName ?? "Player",
                IsBot = mp.Player.PlayerType == PlayerType.Bot,
                PlayerId = mp.PlayerId,
            })
            .ToList();

        return DealReplayResult.Success(new DealReplayResponse
        {
            MatchId = match.Id,
            DealNumber = deal.DealNumber,
            TotalDeals = match.TotalDeals,
            IsRanked = match.IsRanked,
            Dealer = (CorePosition)(int)deal.DealerPosition,
            GameMode = gameMode,
            Multiplier = (CoreMultiplier)(int)deal.Multiplier,
            AnnouncerTeam = deal.AnnouncerTeam.HasValue ? (CoreTeam)(int)deal.AnnouncerTeam.Value : null,
            Team1ScoreBefore = before?.Team1 ?? 0,
            Team2ScoreBefore = before?.Team2 ?? 0,
            Team1MatchPointsEarned = deal.Team1MatchPoints ?? 0,
            Team2MatchPointsEarned = deal.Team2MatchPoints ?? 0,
            Team1CardPoints = assembled.Team1CardPoints,
            Team2CardPoints = assembled.Team2CardPoints,
            WasSweep = deal.WasSweep,
            SweepingTeam = deal.SweepingTeam.HasValue ? (CoreTeam)(int)deal.SweepingTeam.Value : null,
            IsInstantWin = deal.IsInstantWin,
            Players = players,
            Hands = assembled.Hands,
            Tricks = assembled.Tricks,
        });
    }
}
