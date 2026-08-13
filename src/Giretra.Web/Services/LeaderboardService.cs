using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private const int MinGamesForRanking = 2;
    private const int MaxPlayerEntries = 100;
    private const int MaxAchieverEntries = 15;

    private readonly GiretraDbContext _db;

    public LeaderboardService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<LeaderboardResponse> GetLeaderboardAsync()
    {
        var players = await GetPlayerEntriesAsync();
        var topAchievers = await GetTopAchieversAsync();
        var bots = await GetBotEntriesAsync();

        return new LeaderboardResponse
        {
            Players = players,
            TopAchievers = topAchievers,
            Bots = bots,
            PlayerCount = players.Count,
            BotCount = bots.Count,
        };
    }

    public async Task<PlayerProfileResponse?> GetPlayerProfileAsync(Guid playerId)
    {
        var player = await _db.Players
            .Include(p => p.User)
            .Include(p => p.Bot)
            .FirstOrDefaultAsync(p => p.Id == playerId);

        if (player == null)
            return null;

        var achCount = await _db.PlayerAchievements.CountAsync(pa => pa.PlayerId == playerId);

        if (player.PlayerType == PlayerType.Bot)
        {
            return new PlayerProfileResponse
            {
                PlayerId = player.Id,
                DisplayName = player.Bot?.DisplayName ?? "Bot",
                IsBot = true,
                AchievementCount = achCount,
                GamesPlayed = player.GamesPlayed,
                GamesWon = player.GamesWon,
                WinStreak = player.WinStreak,
                BestWinStreak = player.BestWinStreak,
                Description = player.Bot?.Description,
                Author = player.Bot?.Author,
                AuthorGithubUrl = player.Bot?.AuthorGithubUrl,
                Pun = player.Bot?.Pun,
                Difficulty = player.Bot?.Difficulty,
                BotRating = player.Bot?.Rating,
                BotType = player.Bot?.BotType.ToString().ToLowerInvariant(),
            };
        }

        var showElo = player.EloIsPublic;
        return new PlayerProfileResponse
        {
            PlayerId = player.Id,
            DisplayName = player.User?.EffectiveDisplayName ?? "Unknown",
            IsBot = false,
            AchievementCount = achCount,
            GamesPlayed = player.GamesPlayed,
            GamesWon = player.GamesWon,
            WinStreak = player.WinStreak,
            BestWinStreak = player.BestWinStreak,
            AvatarUrl = showElo ? player.User?.AvatarUrl : null,
            EloRating = showElo ? player.EloRating : null,
            MemberSince = player.User?.CreatedAt,
        };
    }

    private async Task<IReadOnlyList<LeaderboardPlayerEntry>> GetPlayerEntriesAsync()
    {
        var topHumans = await _db.Players
            .Include(p => p.User)
            .Where(p => p.PlayerType != PlayerType.Bot && p.GamesPlayed >= MinGamesForRanking)
            .OrderByDescending(p => p.EloRating)
            .ThenByDescending(p => p.GamesWon)
            .Take(MaxPlayerEntries)
            .ToListAsync();

        var entries = topHumans
            .Select(ToPlayerEntry)
            .ToList();

        // Batch-load achievement counts
        var playerIds = entries.Select(e => e.PlayerId).ToList();
        var achCounts = await _db.PlayerAchievements
            .Where(pa => playerIds.Contains(pa.PlayerId))
            .GroupBy(pa => pa.PlayerId)
            .Select(g => new { PlayerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].Rank = i + 1;
            entries[i].AchievementCount = achCounts.GetValueOrDefault(entries[i].PlayerId);
        }

        return entries;
    }

    private async Task<IReadOnlyList<LeaderboardAchieverEntry>> GetTopAchieversAsync()
    {
        // Achievement points = sum of the tiers of a player's earned achievements
        var topByPoints = await _db.PlayerAchievements
            .Where(pa => pa.Player.PlayerType != PlayerType.Bot)
            .GroupBy(pa => pa.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                Points = g.Sum(pa => pa.Achievement.Tier),
                Count = g.Count(),
                Rating = g.Max(pa => pa.Player.EloRating),
            })
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.Count)
            .ThenByDescending(x => x.Rating)
            .Take(MaxAchieverEntries)
            .ToListAsync();

        if (topByPoints.Count == 0)
            return [];

        var playerIds = topByPoints.Select(x => x.PlayerId).ToList();
        var playersById = await _db.Players
            .Include(p => p.User)
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var entries = new List<LeaderboardAchieverEntry>(topByPoints.Count);
        foreach (var row in topByPoints)
        {
            if (!playersById.TryGetValue(row.PlayerId, out var player))
                continue;

            entries.Add(new LeaderboardAchieverEntry
            {
                PlayerId = row.PlayerId,
                Rank = entries.Count + 1,
                DisplayName = player.EloIsPublic
                    ? player.User?.EffectiveDisplayName ?? "Unknown"
                    : "Anonymous Player",
                AvatarUrl = player.EloIsPublic ? player.User?.AvatarUrl : null,
                AchievementPoints = row.Points,
                AchievementCount = row.Count,
            });
        }

        return entries;
    }

    private async Task<IReadOnlyList<LeaderboardBotEntry>> GetBotEntriesAsync()
    {
        var bots = await _db.Players
            .Include(p => p.Bot)
            .Where(p => p.PlayerType == PlayerType.Bot && p.Bot != null && p.Bot.IsActive)
            .ToListAsync();

        var entries = bots
            .Select(ToBotEntry)
            .OrderByDescending(e => e.Rating)
            .ThenByDescending(e => e.WinRate)
            .ToList();

        for (var i = 0; i < entries.Count; i++)
            entries[i].Rank = i + 1;

        return entries;
    }

    private static LeaderboardPlayerEntry ToPlayerEntry(Player p)
    {
        string displayName;
        string? avatarUrl;

        if (p.EloIsPublic)
        {
            displayName = p.User?.EffectiveDisplayName ?? "Unknown";
            avatarUrl = p.User?.AvatarUrl;
        }
        else
        {
            displayName = "Anonymous Player";
            avatarUrl = null;
        }

        return new LeaderboardPlayerEntry
        {
            PlayerId = p.Id,
            Rank = 0,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Rating = p.EloRating,
            GamesPlayed = p.GamesPlayed,
            WinRate = ComputeWinRate(p),
        };
    }

    private static LeaderboardBotEntry ToBotEntry(Player p)
    {
        return new LeaderboardBotEntry
        {
            PlayerId = p.Id,
            Rank = 0,
            DisplayName = p.Bot?.DisplayName ?? "Bot",
            Rating = p.Bot?.Rating ?? p.EloRating,
            GamesPlayed = p.GamesPlayed,
            WinRate = ComputeWinRate(p),
            Author = p.Bot?.Author,
            Difficulty = p.Bot?.Difficulty ?? 0,
        };
    }

    private static double ComputeWinRate(Player p)
    {
        return p.GamesPlayed > 0
            ? Math.Round((double)p.GamesWon / p.GamesPlayed * 100, 1)
            : 0;
    }
}
