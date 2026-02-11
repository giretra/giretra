using System.Security.Claims;
using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class UserSyncService : IUserSyncService
{
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
        var role = roles.Contains("admin") ? UserRole.Admin : UserRole.Normal;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (user == null)
        {
            user = new User
            {
                KeycloakId = keycloakId,
                Username = username,
                DisplayName = displayName,
                Email = email,
                Role = role,
                LastLoginAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Username = username;
            user.DisplayName = displayName;
            user.Email = email;
            user.Role = role;
            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return user;
    }
}
