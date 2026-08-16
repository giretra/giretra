using Giretra.Model;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class AdminGameService : IAdminGameService
{
    private readonly GiretraDbContext _db;

    public AdminGameService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<AdminGameListResponse> GetGamesAsync(Guid? userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Matches.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(m => m.MatchPlayers.Any(mp => mp.Player.UserId == userId));

        var totalCount = await query.CountAsync();

        var games = await query
            .OrderByDescending(m => m.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new AdminGameEntry
            {
                Id = m.Id,
                RoomName = m.RoomName,
                Team1FinalScore = m.Team1FinalScore,
                Team2FinalScore = m.Team2FinalScore,
                WinnerTeam = m.WinnerTeam,
                TotalDeals = m.TotalDeals,
                IsRanked = m.IsRanked,
                WasAbandoned = m.WasAbandoned,
                StartedAt = m.StartedAt,
                CompletedAt = m.CompletedAt,
                DurationSeconds = m.DurationSeconds,
                Players = m.MatchPlayers
                    .OrderBy(mp => mp.Position)
                    .Select(mp => new AdminGamePlayerEntry
                    {
                        DisplayName = mp.Player.User != null
                            ? (mp.Player.User.CustomDisplayName ?? mp.Player.User.DisplayName)
                            : mp.Player.Bot != null ? mp.Player.Bot.DisplayName : "Unknown",
                        UserId = mp.Player.UserId,
                        IsBot = mp.Player.BotId != null,
                        Position = mp.Position,
                        Team = mp.Team,
                        IsWinner = mp.IsWinner,
                        EloChange = mp.EloChange,
                    })
                    .ToList(),
            })
            .ToListAsync();

        return new AdminGameListResponse
        {
            Games = games,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<AdminGameDealsResponse?> GetGameDealsAsync(Guid matchId)
    {
        var exists = await _db.Matches.AsNoTracking().AnyAsync(m => m.Id == matchId);
        if (!exists)
            return null;

        var deals = await _db.Deals.AsNoTracking()
            .Where(d => d.MatchId == matchId)
            .OrderBy(d => d.DealNumber)
            .Select(d => new AdminDealEntry
            {
                DealNumber = d.DealNumber,
                DealerPosition = d.DealerPosition,
                GameMode = d.GameMode,
                AnnouncerTeam = d.AnnouncerTeam,
                Multiplier = d.Multiplier,
                Team1CardPoints = d.Team1CardPoints,
                Team2CardPoints = d.Team2CardPoints,
                Team1MatchPoints = d.Team1MatchPoints,
                Team2MatchPoints = d.Team2MatchPoints,
                WasSweep = d.WasSweep,
                SweepingTeam = d.SweepingTeam,
                IsInstantWin = d.IsInstantWin,
                AnnouncerWon = d.AnnouncerWon,
                CompletedAt = d.CompletedAt,
            })
            .ToListAsync();

        return new AdminGameDealsResponse { Deals = deals };
    }
}
