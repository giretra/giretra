using Giretra.Model;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class MatchHistoryService : IMatchHistoryService
{
    private readonly GiretraDbContext _db;

    public MatchHistoryService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<MatchHistoryListResponse> GetMatchHistoryAsync(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // Find the player record for this user
        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
        {
            return new MatchHistoryListResponse
            {
                Matches = [],
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        var query = _db.MatchPlayers
            .Where(mp => mp.PlayerId == player.Id)
            .Include(mp => mp.Match)
                .ThenInclude(m => m.MatchPlayers)
                    .ThenInclude(mp => mp.Player)
                        .ThenInclude(p => p.User)
            .Include(mp => mp.Match)
                .ThenInclude(m => m.MatchPlayers)
                    .ThenInclude(mp => mp.Player)
                        .ThenInclude(p => p.Bot)
            .Where(mp => mp.Match.CompletedAt != null)
            .OrderByDescending(mp => mp.Match.CompletedAt);

        var totalCount = await query.CountAsync();

        var matchPlayers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var matches = matchPlayers.Select(mp =>
        {
            var match = mp.Match;
            var players = match.MatchPlayers
                .OrderBy(p => p.Position)
                .Select(p =>
                {
                    var displayName = p.Player.User?.DisplayName
                        ?? p.Player.Bot?.DisplayName
                        ?? "Unknown";
                    return new MatchHistoryPlayerResponse
                    {
                        DisplayName = displayName,
                        Position = p.Position,
                        Team = p.Team,
                        IsWinner = p.IsWinner
                    };
                })
                .ToList();

            return new MatchHistoryItemResponse
            {
                MatchId = match.Id,
                RoomName = match.RoomName,
                Team1FinalScore = match.Team1FinalScore,
                Team2FinalScore = match.Team2FinalScore,
                Team = mp.Team,
                Position = mp.Position,
                IsWinner = mp.IsWinner,
                EloChange = mp.EloChange,
                TotalDeals = match.TotalDeals,
                WasAbandoned = match.WasAbandoned,
                DurationSeconds = match.DurationSeconds,
                PlayedAt = match.CompletedAt ?? match.StartedAt,
                Players = players
            };
        }).ToList();

        return new MatchHistoryListResponse
        {
            Matches = matches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
