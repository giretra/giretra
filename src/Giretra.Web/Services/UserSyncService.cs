using System.Security.Claims;
using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class UserSyncService : IUserSyncService
{
    // How stale LastLoginAt may get before we pay a write transaction to refresh it
    private static readonly TimeSpan LastLoginRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly GiretraDbContext _db;

    public UserSyncService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<User> SyncUserAsync(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Missing 'sub' claim");

        var keycloakId = Guid.Parse(sub);
        var username = principal.FindFirstValue("preferred_username") ?? sub;
        var displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? sub;
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");

        // Determine role from realm_role claims (set by KeycloakClaimsTransformation)
        var roles = principal.FindAll("realm_role").Select(c => c.Value).ToList();
        var role = roles.Contains("admin") ? UserRole.Admin
            : roles.Contains("moderator") ? UserRole.Moderator
            : UserRole.Normal;

        try
        {
            return await SyncUserCoreAsync(keycloakId, username, displayName, email, role);
        }
        catch (DbUpdateException)
        {
            // Concurrent request already created this user (unique constraint on KeycloakId).
            // Detach all tracked entities and retry — the user now exists.
            _db.ChangeTracker.Clear();
            return await SyncUserCoreAsync(keycloakId, username, displayName, email, role);
        }
    }

    private async Task<User> SyncUserCoreAsync(Guid keycloakId, string username, string displayName, string? email, UserRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        // Only a user not found by KeycloakId (new, or a re-created Keycloak
        // account relinked by email) can be missing its Player record
        var ensurePlayer = user == null;

        // If not found by KeycloakId, check by email to handle re-created Keycloak accounts
        if (user == null && email != null)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.KeycloakId = keycloakId;
            }
        }

        if (user == null)
        {
            user = new User
            {
                KeycloakId = keycloakId,
                Username = username,
                DisplayName = displayName,
                Email = email,
                Role = role,
                LastLoginAt = now
            };
            _db.Users.Add(user);
        }
        else
        {
            var changed = user.Username != username || user.Email != email || user.Role != role;
            user.Username = username;
            user.Email = email;
            user.Role = role;

            // LastLoginAt would dirty the entity on every request; refresh it at
            // most once per interval so routine reads don't pay a write transaction
            if (changed || now - user.LastLoginAt >= LastLoginRefreshInterval)
            {
                user.LastLoginAt = now;
                user.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();

        if (!ensurePlayer)
            return user;

        // Ensure Player record exists for this user
        var hasPlayer = await _db.Players.AnyAsync(p => p.UserId == user.Id);
        if (!hasPlayer)
        {
            try
            {
                _db.Players.Add(new Player
                {
                    PlayerType = PlayerType.Human,
                    UserId = user.Id,
                    EloRating = 1000,
                    EloIsPublic = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Concurrent request already created the Player record
                _db.ChangeTracker.Clear();
            }
        }

        return user;
    }
}
