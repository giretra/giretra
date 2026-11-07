using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Rendering;

/// <summary>Offline mode: renders the in-memory clips of <see cref="OfflineClipService"/> in-process.</summary>
public sealed class InMemoryClipJobQueue : IClipJobQueue
{
    private readonly OfflineClipService _clips;
    private readonly ClipRenderOptions _options;
    private readonly object _gate = new();

    public InMemoryClipJobQueue(OfflineClipService clips, IOptions<ClipRenderOptions> options)
    {
        _clips = clips;
        _options = options.Value;
    }

    public Task<ClipJob?> ClaimAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            var next = _clips.All
                .Where(c => c.Status == ClipStatusResponse.Pending)
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefault();
            if (next == null)
                return Task.FromResult<ClipJob?>(null);
            next.Status = ClipStatusResponse.Rendering;
            next.Attempts++;
            return Task.FromResult<ClipJob?>(new ClipJob(Guid.Empty, next.ShortId, next.Locale, next.Attempts));
        }
    }

    public Task CompleteAsync(ClipJob job, ClipRenderOutput output, CancellationToken ct)
    {
        var clip = _clips.Find(job.ShortId);
        if (clip != null && clip.Status != ClipStatusResponse.Removed)
        {
            clip.Status = ClipStatusResponse.Ready;
            clip.HasPoster = output.HasPoster;
            clip.HasVideo = output.HasVideo;
            clip.DurationMs = output.DurationMs;
            clip.RenderedAt = DateTimeOffset.UtcNow;
            clip.Error = null;
        }
        return Task.CompletedTask;
    }

    public Task FailAsync(ClipJob job, string error, CancellationToken ct)
    {
        var clip = _clips.Find(job.ShortId);
        if (clip != null && clip.Status != ClipStatusResponse.Removed)
        {
            clip.Error = error;
            clip.Status = clip.Attempts >= _options.MaxAttempts ? ClipStatusResponse.Failed : ClipStatusResponse.Pending;
        }
        return Task.CompletedTask;
    }
}
