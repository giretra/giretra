using System.Security.Claims;
using Giretra.Model.Entities;
using Giretra.Web.Services;

namespace Giretra.Web.Middleware;

public sealed class UserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public UserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserSyncService userSyncService, UserSyncCache cache)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            User? user;
            if (Guid.TryParse(sub, out var keycloakId))
            {
                if (!cache.TryGet(keycloakId, out user!))
                {
                    user = await userSyncService.SyncUserAsync(context.User);
                    cache.Set(keycloakId, user);
                }
            }
            else
            {
                user = await userSyncService.SyncUserAsync(context.User);
            }

            if (user.IsBanned)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Account is banned.");
                return;
            }

            context.Items["GiretraUser"] = user;
        }

        await _next(context);
    }
}
