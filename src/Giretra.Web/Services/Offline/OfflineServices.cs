using System.Collections.Concurrent;
using System.Security.Claims;
using Giretra.Core.Players;
using Giretra.Model.Entities;
using Giretra.Web.Domain;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Elo;
using UserRole = Giretra.Model.Enums.UserRole;

namespace Giretra.Web.Services.Offline;

/// <summary>
/// In-memory user sync that creates User entities on the fly (no database).
/// </summary>
public sealed class OfflineUserSyncService : IUserSyncService
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User> SyncUserAsync(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No sub claim found");

        var userId = Guid.Parse(sub);
        var username = principal.FindFirstValue("preferred_username") ?? "offline";
        var name = principal.FindFirstValue("name") ?? username;
        var email = principal.FindFirstValue("email");

        var user = _users.GetOrAdd(userId, _ => new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = userId,
            Username = username,
            DisplayName = name,
            Email = email,
            Role = UserRole.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        // Update last login
        user.LastLoginAt = DateTimeOffset.UtcNow;

        return Task.FromResult(user);
    }

    /// <summary>
    /// Looks up a previously synced user by their Keycloak (deterministic) GUID.
    /// </summary>
    public User? FindByKeycloakId(Guid keycloakId)
        => _users.TryGetValue(keycloakId, out var user) ? user : null;
}

/// <summary>
/// No-op match persistence (no database to write to).
/// </summary>
public sealed class OfflineMatchPersistenceService : IMatchPersistenceService
{
    public Task PersistCompletedMatchAsync(GameSession session) => Task.CompletedTask;
    public Task PersistAbandonedMatchAsync(GameSession session, PlayerPosition abandonerPosition) => Task.CompletedTask;
}

/// <summary>
/// No-op ELO service (no database to update).
/// </summary>
public sealed class OfflineEloService : IEloService
{
    public Task StageMatchEloAsync(Guid matchId, GameSession session) => Task.CompletedTask;
    public Task StageAbandonEloAsync(Guid matchId, GameSession session, PlayerPosition abandonerPosition) => Task.CompletedTask;
}

/// <summary>
/// Stub profile service returning minimal data from in-memory user.
/// </summary>
public sealed class OfflineProfileService : IProfileService
{
    private readonly OfflineUserSyncService _userSync;

    public OfflineProfileService(OfflineUserSyncService userSync)
    {
        _userSync = userSync;
    }

    public Task<ProfileResponse> GetProfileAsync(Guid userId)
    {
        // Try to find a user that was synced with this keycloak ID
        var user = _userSync.FindByKeycloakId(userId);

        return Task.FromResult(new ProfileResponse
        {
            Username = user?.Username ?? "offline",
            DisplayName = user?.EffectiveDisplayName ?? "Offline User",
            EloRating = 1000,
            EloIsPublic = true,
            GamesPlayed = 0,
            GamesWon = 0,
            WinStreak = 0,
            BestWinStreak = 0,
            CreatedAt = user?.CreatedAt ?? DateTimeOffset.UtcNow,
        });
    }

    public Task<PlayerProfileResponse?> GetPlayerProfileAsync(string roomId, PlayerPosition position, Guid requestingUserId)
        => Task.FromResult<PlayerProfileResponse?>(null);

    public Task<(bool Success, string? Error)> UpdateDisplayNameAsync(Guid userId, string displayName)
        => Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? AvatarUrl, string? Error)> UpdateAvatarAsync(Guid userId, IFormFile file)
        => Task.FromResult((false, (string?)null, (string?)"Not available offline"));

    public Task DeleteAvatarAsync(Guid userId) => Task.CompletedTask;

    public Task UpdateEloVisibilityAsync(Guid userId, bool isPublic) => Task.CompletedTask;
}

/// <summary>
/// Stub friend service returning empty data.
/// </summary>
public sealed class OfflineFriendService : IFriendService
{
    public Task<FriendsListResponse> GetFriendsAsync(Guid userId)
        => Task.FromResult(new FriendsListResponse
        {
            Friends = [],
            PendingReceived = [],
            PendingSent = [],
        });

    public Task<(bool Success, string? Error)> SendFriendRequestAsync(Guid userId, string username)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> AcceptFriendRequestAsync(Guid userId, Guid friendshipId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> DeclineFriendRequestAsync(Guid userId, Guid friendshipId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> RemoveFriendAsync(Guid userId, Guid friendUserId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<UserSearchResponse> SearchUsersAsync(Guid userId, string query)
        => Task.FromResult(new UserSearchResponse { Results = [] });

    public Task<int> GetPendingCountAsync(Guid userId)
        => Task.FromResult(0);
}

/// <summary>
/// Stub block service returning empty data.
/// </summary>
public sealed class OfflineBlockService : IBlockService
{
    public Task<IReadOnlyList<BlockedUserResponse>> GetBlockedUsersAsync(Guid userId)
        => Task.FromResult<IReadOnlyList<BlockedUserResponse>>([]);

    public Task<(bool Success, string? Error)> BlockUserAsync(Guid userId, string username, string? reason)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> UnblockUserAsync(Guid userId, Guid blockId)
        => Task.FromResult((false, (string?)"Not available offline"));
}

/// <summary>
/// Stub match history service returning empty data.
/// </summary>
public sealed class OfflineMatchHistoryService : IMatchHistoryService
{
    public Task<MatchHistoryListResponse> GetMatchHistoryAsync(Guid userId, int page, int pageSize)
        => Task.FromResult(new MatchHistoryListResponse
        {
            Matches = [],
            TotalCount = 0,
            Page = page,
            PageSize = pageSize,
        });
}

/// <summary>
/// Stub leaderboard service returning empty data.
/// </summary>
public sealed class OfflineLeaderboardService : ILeaderboardService
{
    public Task<LeaderboardResponse> GetLeaderboardAsync()
        => Task.FromResult(new LeaderboardResponse
        {
            Players = [],
            Bots = [],
            PlayerCount = 0,
            BotCount = 0,
        });

    public Task<PlayerProfileResponse?> GetPlayerProfileAsync(Guid playerId)
        => Task.FromResult<PlayerProfileResponse?>(null);
}

/// <summary>
/// Extension methods to register all offline service stubs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOfflineServices(this IServiceCollection services)
    {
        // OfflineUserSyncService is registered as singleton so OfflineProfileService can access it
        var userSync = new OfflineUserSyncService();
        services.AddSingleton(userSync);
        services.AddSingleton<IUserSyncService>(userSync);

        services.AddSingleton<IMatchPersistenceService, OfflineMatchPersistenceService>();
        services.AddSingleton<IEloService, OfflineEloService>();
        services.AddSingleton<IProfileService>(sp => new OfflineProfileService(sp.GetRequiredService<OfflineUserSyncService>()));
        services.AddSingleton<IFriendService, OfflineFriendService>();
        services.AddSingleton<IBlockService, OfflineBlockService>();
        services.AddSingleton<IMatchHistoryService, OfflineMatchHistoryService>();
        services.AddSingleton<ILeaderboardService, OfflineLeaderboardService>();

        return services;
    }
}
