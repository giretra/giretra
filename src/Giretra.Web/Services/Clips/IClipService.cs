using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services.Clips;

public enum ClipCreateStatus
{
    /// <summary>A new clip was queued.</summary>
    Created,
    /// <summary>An identical clip already existed; it is returned.</summary>
    Existing,
    InvalidRequest,
    MatchNotFound,
    Forbidden,
    NotReplayable,
    Throttled,
}

public sealed record ClipCreateResult(ClipCreateStatus Status, ClipResponse? Clip, string? Error = null);

public enum ClipDeleteStatus { Ok, NotFound, Forbidden }

/// <summary>Absolute origin (scheme + host) the public URLs are built on.</summary>
public readonly record struct PublicOrigin(string Value)
{
    public string Url(string path) => Value.TrimEnd('/') + path;
}

public interface IClipService
{
    Task<ClipCreateResult> CreateAsync(Guid userId, CreateClipRequest request, PublicOrigin origin);

    /// <summary>Public view of a clip with its replay payload; null when unknown.</summary>
    Task<ClipResponse?> GetPublicAsync(string shortId, PublicOrigin origin, bool includeReplay = true);

    Task<ClipListResponse> ListMineAsync(Guid userId, PublicOrigin origin);

    Task<ClipDeleteStatus> DeleteAsync(Guid userId, string shortId, bool isModerator);

    /// <summary>Counts a landing-page view (best effort).</summary>
    Task RecordViewAsync(string shortId);

    /// <summary>Moderation list, newest first; <paramref name="status"/> null means every status.</summary>
    Task<AdminClipListResponse> ListForAdminAsync(ClipStatusResponse? status, int page, int pageSize, PublicOrigin origin);

    /// <summary>Queues the clip again from scratch (attempts reset); false when unknown or removed.</summary>
    Task<bool> RetryAsync(string shortId);
}
