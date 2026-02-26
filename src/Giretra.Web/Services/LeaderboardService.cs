using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class LeaderboardService : ILeaderboardService
{
    private const int MinGamesForRanking = 5;
    private const int MaxPlayerEntries = 100;

    private readonly GiretraDbContext _db;

    public LeaderboardService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<LeaderboardResponse> GetLeaderboardAsync()
    {
        var players = await GetPlayerEntriesAsync();
        var bots = await GetBotEntriesAsync();

        return new LeaderboardResponse
        {
            Players = players,
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

        if (player.PlayerType == PlayerType.Bot)
        {
            return new PlayerProfileResponse
            {
                DisplayName = player.Bot?.DisplayName ?? "Bot",
                IsBot = true,
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
            DisplayName = player.User?.EffectiveDisplayName ?? "Unknown",
            IsBot = false,
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

        for (var i = 0; i < entries.Count; i++)
            entries[i].Rank = i + 1;

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
