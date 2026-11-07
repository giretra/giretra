using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Giretra.Web.Endpoints;

/// <summary>
/// Public, anonymous pages of a clip: the landing page with Open Graph tags, the poster and
/// the video. Mapped before the SPA fallback so scrapers never see the Angular shell.
/// </summary>
public static class ClipPagesEndpoints
{
    private const string Immutable = "public, max-age=31536000, immutable";

    public static IEndpointRouteBuilder MapClipPages(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/c").AllowAnonymous();

        group.MapGet("/{shortId}", async (string shortId, HttpContext http, IClipService clips) =>
        {
            if (!ShortId.IsValid(shortId))
                return Results.NotFound();

            var origin = ClipPublicOrigin.Resolve(http);
            var clip = await clips.GetPublicAsync(shortId, origin, includeReplay: false);
            if (clip == null)
                return Results.NotFound();

            if (clip.Status is ClipStatusResponse.Ready)
                _ = SafeRecordViewAsync(clips, shortId, http.RequestServices.GetRequiredService<ILoggerFactory>());

            http.Response.Headers.CacheControl = "no-cache";
            var status = clip.Status == ClipStatusResponse.Removed ? StatusCodes.Status410Gone : StatusCodes.Status200OK;
            return Results.Content(ClipLandingPage.Render(clip, origin), "text/html; charset=utf-8", statusCode: status);
        });

        group.MapGet("/{shortId}/poster.png", (string shortId, ClipStorage storage) =>
            ServeFile(storage.PosterPath(shortId), "image/png", shortId, download: null));

        group.MapGet("/{shortId}/video.mp4", (string shortId, [FromQuery] int? download, ClipStorage storage) =>
            ServeFile(storage.VideoPath(shortId), "video/mp4", shortId, download == 1 ? $"giretra-{shortId}.mp4" : null));

        return app;
    }

    private static IResult ServeFile(string path, string contentType, string shortId, string? download)
    {
        if (!ShortId.IsValid(shortId) || !File.Exists(path))
            return Results.NotFound();

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        var lastModified = File.GetLastWriteTimeUtc(path);
        return new CachedFileResult(stream, contentType, download, lastModified);
    }

    private static async Task SafeRecordViewAsync(IClipService clips, string shortId, ILoggerFactory loggers)
    {
        try
        {
            await clips.RecordViewAsync(shortId);
        }
        catch (Exception ex)
        {
            loggers.CreateLogger(nameof(ClipPagesEndpoints)).LogDebug(ex, "View count update failed for {ShortId}", shortId);
        }
    }

    /// <summary>File result with range support and a long immutable cache (URLs carry the short id).</summary>
    private sealed class CachedFileResult(Stream stream, string contentType, string? download, DateTime lastModified) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = Immutable;
            var inner = Results.File(stream, contentType, download, new DateTimeOffset(lastModified), entityTag: null, enableRangeProcessing: true);
            return inner.ExecuteAsync(httpContext);
        }
    }
}
