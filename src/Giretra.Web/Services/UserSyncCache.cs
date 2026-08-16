using Giretra.Model.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Giretra.Web.Services;

/// <summary>
/// Short-lived cache of synced users keyed by Keycloak id, so that
/// <see cref="Middleware.UserSyncMiddleware"/> doesn't hit the database on every
/// authenticated request. Services that mutate fields read from the cached user
/// (ban status, display name) must call <see cref="Invalidate"/> after saving.
/// </summary>
public sealed class UserSyncCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public bool TryGet(Guid keycloakId, out User user)
    {
        return _cache.TryGetValue(keycloakId, out user!);
    }

    public void Set(Guid keycloakId, User user)
    {
        _cache.Set(keycloakId, user, Ttl);
    }

    public void Invalidate(Guid keycloakId)
    {
        _cache.Remove(keycloakId);
    }
}
