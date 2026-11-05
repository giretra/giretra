namespace Giretra.Web.Services.Rendering;

public sealed record ClipJob(Guid Id, string ShortId, string Locale, int Attempts);

public sealed record ClipRenderOutput(bool HasPoster, bool HasVideo, int? DurationMs);

/// <summary>Claims pending clips for rendering and records the outcome.</summary>
public interface IClipJobQueue
{
    /// <summary>Claims the oldest pending clip, re-queuing stale claims first; null when idle.</summary>
    Task<ClipJob?> ClaimAsync(CancellationToken ct);

    Task CompleteAsync(ClipJob job, ClipRenderOutput output, CancellationToken ct);

    /// <summary>Records a failure; the clip goes back to pending until the attempt cap is reached.</summary>
    Task FailAsync(ClipJob job, string error, CancellationToken ct);
}
