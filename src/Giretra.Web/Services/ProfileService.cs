using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Giretra.Web.Repositories;
using Giretra.Web.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlayerPosition = Giretra.Core.Players.PlayerPosition;

namespace Giretra.Web.Services;

public sealed class ProfileService : IProfileService
{
    private readonly GiretraDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IRoomRepository _rooms;

    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private const long MaxAvatarSize = 2 * 1024 * 1024; // 2MB

    public ProfileService(GiretraDbContext db, IWebHostEnvironment env, IRoomRepository rooms)
    {
        _db = db;
        _env = env;
        _rooms = rooms;
    }

    public async Task<ProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.Player)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("User not found");

        // Create Player record if missing
        if (user.Player == null)
        {
            user.Player = new Player
            {
                PlayerType = PlayerType.Human,
                UserId = user.Id,
                EloRating = 1000,
                EloIsPublic = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Players.Add(user.Player);
            await _db.SaveChangesAsync();
        }

        return new ProfileResponse
        {
            Username = user.Username,
            DisplayName = user.EffectiveDisplayName,
            AvatarUrl = user.AvatarUrl,
            EloRating = user.Player.EloRating,
            EloIsPublic = user.Player.EloIsPublic,
            GamesPlayed = user.Player.GamesPlayed,
            GamesWon = user.Player.GamesWon,
            WinStreak = user.Player.WinStreak,
            BestWinStreak = user.Player.BestWinStreak,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<PlayerProfileResponse?> GetPlayerProfileAsync(string roomId, PlayerPosition position, Guid requestingUserId)
    {
        var room = _rooms.GetById(roomId);
        if (room == null)
            return null;

        // Check if it's a human player
        var client = room.PlayerSlots.GetValueOrDefault(position);
        if (client?.UserId != null)
        {
            var user = await _db.Users
                .Include(u => u.Player)
                .FirstOrDefaultAsync(u => u.Id == client.UserId);

            if (user == null)
                return null;

            var player = user.Player;
            var isSelf = user.Id == requestingUserId;
            var showElo = player != null && (player.EloIsPublic || isSelf);

            return new PlayerProfileResponse
            {
                DisplayName = user.EffectiveDisplayName,
                IsBot = false,
                GamesPlayed = player?.GamesPlayed ?? 0,
                GamesWon = player?.GamesWon ?? 0,
                WinStreak = player?.WinStreak ?? 0,
                BestWinStreak = player?.BestWinStreak ?? 0,
                AvatarUrl = user.AvatarUrl,
                EloRating = showElo ? player?.EloRating : null,
                MemberSince = user.CreatedAt,
            };
        }

        // Check if it's a bot
        if (room.AiSlots.TryGetValue(position, out var agentType))
        {
            var bot = await _db.Bots
                .Include(b => b.Player)
                .FirstOrDefaultAsync(b => b.AgentType == agentType);

            if (bot == null)
                return null;

            return new PlayerProfileResponse
            {
                DisplayName = bot.DisplayName,
                IsBot = true,
                GamesPlayed = bot.Player?.GamesPlayed ?? 0,
                GamesWon = bot.Player?.GamesWon ?? 0,
                WinStreak = bot.Player?.WinStreak ?? 0,
                BestWinStreak = bot.Player?.BestWinStreak ?? 0,
                Description = bot.Description,
                Author = bot.Author,
                AuthorGithubUrl = bot.AuthorGithubUrl,
                Pun = bot.Pun,
                Difficulty = bot.Difficulty,
                BotRating = bot.Rating,
                BotType = bot.BotType.ToString().ToLowerInvariant(),
            };
        }

        // Seat is empty
        return null;
    }

    public async Task<(bool Success, string? Error)> UpdateDisplayNameAsync(Guid userId, string displayName)
    {
        var trimmed = DisplayNameValidator.Trim(displayName);
        var (isValid, error) = DisplayNameValidator.Validate(trimmed);
        if (!isValid)
            return (false, error);

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        user.CustomDisplayName = trimmed;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? AvatarUrl, string? Error)> UpdateAvatarAsync(Guid userId, IFormFile file)
    {
        if (file.Length == 0)
            return (false, null, "File is empty.");

        if (file.Length > MaxAvatarSize)
            return (false, null, "File size must be 2MB or less.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return (false, null, "Only JPG and PNG files are allowed.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, null, "User not found.");

        // Delete old avatar if exists
        DeleteAvatarFile(user.AvatarUrl);

        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{userId}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var avatarUrl = $"/uploads/avatars/{fileName}";
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return (true, avatarUrl, null);
    }

    public async Task DeleteAvatarAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return;

        DeleteAvatarFile(user.AvatarUrl);

        user.AvatarUrl = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateEloVisibilityAsync(Guid userId, bool isPublic)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
        {
            // Create Player record if missing
            player = new Player
            {
                PlayerType = PlayerType.Human,
                UserId = userId,
                EloRating = 1000,
                EloIsPublic = isPublic,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Players.Add(player);
        }
        else
        {
            player.EloIsPublic = isPublic;
            player.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    private void DeleteAvatarFile(string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
            return;

        var webRoot = _env.WebRootPath ?? "wwwroot";
        var filePath = Path.Combine(webRoot, avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
