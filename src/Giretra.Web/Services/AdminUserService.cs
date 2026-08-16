using Giretra.Model;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly GiretraDbContext _db;
    private readonly UserSyncCache _userSyncCache;

    public AdminUserService(GiretraDbContext db, UserSyncCache userSyncCache)
    {
        _db = db;
        _userSyncCache = userSyncCache;
    }

    public async Task<AdminUserListResponse> GetUsersAsync(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, pattern) ||
                EF.Functions.ILike(u.DisplayName, pattern) ||
                (u.CustomDisplayName != null && EF.Functions.ILike(u.CustomDisplayName, pattern)) ||
                (u.Email != null && EF.Functions.ILike(u.Email, pattern)));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.LastLoginAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserEntry
            {
                Id = u.Id,
                PlayerId = u.Player != null ? u.Player.Id : null,
                Username = u.Username,
                DisplayName = u.CustomDisplayName ?? u.DisplayName,
                CustomDisplayName = u.CustomDisplayName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                Role = u.Role,
                IsBanned = u.IsBanned,
                BanReason = u.BanReason,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                EloRating = u.Player != null ? u.Player.EloRating : null,
                GamesPlayed = u.Player != null ? u.Player.GamesPlayed : null,
                GamesWon = u.Player != null ? u.Player.GamesWon : null,
                BlockedByCount = u.BlocksReceived.Count,
            })
            .ToListAsync();

        return new AdminUserListResponse
        {
            Users = users,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<(bool Success, string? Error)> BanAsync(Guid userId, string? reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        // Role comes from Keycloak; staff accounts are managed there, not banned here
        if (user.Role != UserRole.Normal)
            return (false, "Moderators and admins cannot be banned.");

        user.IsBanned = true;
        user.BanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _userSyncCache.Invalidate(user.KeycloakId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UnbanAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        user.IsBanned = false;
        user.BanReason = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _userSyncCache.Invalidate(user.KeycloakId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ClearDisplayNameAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (false, "User not found.");

        user.CustomDisplayName = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _userSyncCache.Invalidate(user.KeycloakId);
        return (true, null);
    }
}
