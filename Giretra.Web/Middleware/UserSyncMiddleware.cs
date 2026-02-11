using Giretra.Web.Services;

namespace Giretra.Web.Middleware;

public sealed class UserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public UserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserSyncService userSyncService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userSyncService.SyncUserAsync(context.User);

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
