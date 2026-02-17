using Giretra.Model;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly GiretraDbContext _db;

    public LeaderboardService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<LeaderboardResponse> GetLeaderboardAsync()
    {
        var eligible = _db.Players
            .Include(p => p.User)
            .Include(p => p.Bot)
            .Where(p => p.GamesPlayed >= 5);

        var totalCount = await eligible.CountAsync();

        var top = await eligible
            .OrderByDescending(p => p.EloRating)
            .ThenByDescending(p => p.GamesWon)
            .Take(100)
            .ToListAsync();

        var entries = top.Select((p, i) =>
        {
            string displayName;
            string? avatarUrl;
            bool isBot;

            if (p.PlayerType == PlayerType.Bot)
            {
                displayName = p.Bot?.DisplayName ?? "Bot";
                avatarUrl = null;
                isBot = true;
            }
            else if (p.EloIsPublic)
            {
                displayName = p.User?.EffectiveDisplayName ?? "Unknown";
                avatarUrl = p.User?.AvatarUrl;
                isBot = false;
            }
            else
            {
                displayName = "Anonymous Player";
                avatarUrl = null;
                isBot = false;
            }

            return new LeaderboardEntryResponse
            {
                Rank = i + 1,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
                Rating = p.EloRating,
                GamesPlayed = p.GamesPlayed,
                WinRate = Math.Round((double)p.GamesWon / p.GamesPlayed * 100, 1),
                IsBot = isBot,
            };
        }).ToList();

        return new LeaderboardResponse
        {
            Entries = entries,
            TotalCount = totalCount,
        };
    }
}
