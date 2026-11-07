using Giretra.Model;
using Giretra.Web.Services.Clips;
using Serilog;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// Entry point of the <c>--render-worker</c> process: database, clip storage, the Playwright
/// renderer and the render loop, plus a health endpoint. None of the game services run here.
/// </summary>
public static class RenderWorkerHost
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();

        builder.Services.AddGiretraDb();
        builder.Services.Configure<ClipOptions>(builder.Configuration.GetSection(ClipOptions.Section));
        builder.Services.Configure<ClipRenderOptions>(builder.Configuration.GetSection(ClipRenderOptions.Section));
        builder.Services.PostConfigure<ClipRenderOptions>(o => o.Enabled = true);
        builder.Services.AddSingleton<ClipStorage>();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<ClipSoundBank>();
        builder.Services.AddSingleton<ClipRenderer>();
        builder.Services.AddScoped<IClipJobQueue, PostgresClipJobQueue>();
        builder.Services.AddScoped<IClipRetention, PostgresClipRetention>();
        builder.Services.AddHostedService<ClipRenderHostedService>();
        builder.Services.AddHostedService<ClipRetentionHostedService>();

        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", role = "render-worker" }));

        Log.Information("Starting Giretra.Web render worker");
        await app.RunAsync();
    }
}
