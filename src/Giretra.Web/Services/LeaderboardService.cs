using Giretra.Model;
using Giretra.Model.Entities;
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
        var bots = await _db.Players
            .Include(p => p.Bot)
            .Where(p => p.PlayerType == PlayerType.Bot)
            .ToListAsync();

        var eligibleHumans = _db.Players
            .Include(p => p.User)
            .Where(p => p.PlayerType != PlayerType.Bot && p.GamesPlayed >= 5);

        var humanCount = await eligibleHumans.CountAsync();

        var topHumans = await eligibleHumans
            .OrderByDescending(p => p.EloRating)
            .ThenByDescending(p => p.GamesWon)
            .Take(100)
            .ToListAsync();

        var entries = bots
            .Select(p => ToEntry(p, isBot: true, rating: p.Bot?.Rating ?? p.EloRating))
            .Concat(topHumans.Select(p => ToEntry(p, isBot: false, rating: p.EloRating)))
            .OrderByDescending(e => e.Rating)
            .ThenByDescending(e => e.WinRate)
            .ToList();

        for (var i = 0; i < entries.Count; i++)
            entries[i].Rank = i + 1;

        return new LeaderboardResponse
        {
            Entries = entries,
            TotalCount = humanCount + bots.Count,
        };
    }

    private static LeaderboardEntryResponse ToEntry(Player p, bool isBot, int rating)
    {
        string displayName;
        string? avatarUrl;

        if (isBot)
        {
            displayName = p.Bot?.DisplayName ?? "Bot";
            avatarUrl = null;
        }
        else if (p.EloIsPublic)
        {
            displayName = p.User?.EffectiveDisplayName ?? "Unknown";
            avatarUrl = p.User?.AvatarUrl;
        }
        else
        {
            displayName = "Anonymous Player";
            avatarUrl = null;
        }

        return new LeaderboardEntryResponse
        {
            Rank = 0,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Rating = rating,
            GamesPlayed = p.GamesPlayed,
            WinRate = p.GamesPlayed > 0
                ? Math.Round((double)p.GamesWon / p.GamesPlayed * 100, 1)
                : 0,
            IsBot = isBot,
        };
    }
}
