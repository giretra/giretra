using System.Collections.Concurrent;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;
using CorePosition = Giretra.Core.Players.PlayerPosition;
using CoreTeam = Giretra.Core.Players.Team;

namespace Giretra.Web.Services.Clips;

/// <summary>
/// In-memory clips for <c>--offline</c> mode (no database). Also feeds the in-process render
/// loop through <see cref="InMemoryClipJobQueue"/> so the whole share flow runs locally.
/// </summary>
public sealed class OfflineClipService : IClipService
{
    public sealed class OfflineClip
    {
        public required string ShortId { get; init; }
        public required CreateClipRequest Request { get; init; }
        public required CorePosition View { get; init; }
        public required string Locale { get; init; }
        public ClipStatusResponse Status { get; set; } = ClipStatusResponse.Pending;
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? RenderedAt { get; set; }
        public int? DurationMs { get; set; }
        public bool HasPoster { get; set; }
        public bool HasVideo { get; set; }
        public int ViewCount { get; set; }
        public string? Error { get; set; }
        public int Attempts { get; set; }
    }

    private readonly ConcurrentDictionary<string, OfflineClip> _clips = new();
    private readonly IDealReplayService _replays;

    public OfflineClipService(IDealReplayService replays)
    {
        _replays = replays;
    }

    public IReadOnlyCollection<OfflineClip> All => _clips.Values.ToList();

    public OfflineClip? Find(string shortId) => _clips.GetValueOrDefault(shortId);

    public async Task<ClipCreateResult> CreateAsync(Guid userId, CreateClipRequest request, PublicOrigin origin)
    {
        if (request.FromTrick < 1 || request.ToTrick > 8 || request.FromTrick > request.ToTrick)
            return new ClipCreateResult(ClipCreateStatus.InvalidRequest, null, "Trick range must be within 1..8.");

        var replay = await _replays.GetReplayAsync(request.MatchId, request.DealNumber, userId);
        if (replay.Status != DealReplayStatus.Ok)
            return new ClipCreateResult(ClipCreateStatus.MatchNotFound, null);

        var view = request.ViewPosition ?? CorePosition.Bottom;
        var locale = ClipTexts.Normalize(request.Locale);
        var existing = _clips.Values.FirstOrDefault(c =>
            c.Request.MatchId == request.MatchId && c.Request.DealNumber == request.DealNumber
            && c.Request.FromTrick == request.FromTrick && c.Request.ToTrick == request.ToTrick
            && c.View == view && c.Request.ShowAllHands == request.ShowAllHands && c.Locale == locale);
        if (existing != null)
            return new ClipCreateResult(ClipCreateStatus.Existing, await ToResponseAsync(existing, origin, false));

        var clip = new OfflineClip { ShortId = ShortId.Generate(), Request = request, View = view, Locale = locale };
        _clips[clip.ShortId] = clip;
        return new ClipCreateResult(ClipCreateStatus.Created, await ToResponseAsync(clip, origin, false));
    }

    public async Task<ClipResponse?> GetPublicAsync(string shortId, PublicOrigin origin, bool includeReplay = true)
    {
        var clip = Find(shortId);
        return clip == null ? null : await ToResponseAsync(clip, origin, includeReplay && clip.Status != ClipStatusResponse.Removed);
    }

    public async Task<ClipListResponse> ListMineAsync(Guid userId, PublicOrigin origin)
    {
        var items = new List<ClipResponse>();
        foreach (var clip in _clips.Values.Where(c => c.Status != ClipStatusResponse.Removed).OrderByDescending(c => c.CreatedAt))
            items.Add(await ToResponseAsync(clip, origin, false));
        return new ClipListResponse { Items = items };
    }

    public Task<ClipDeleteStatus> DeleteAsync(Guid userId, string shortId, bool isModerator)
    {
        var clip = Find(shortId);
        if (clip == null || clip.Status == ClipStatusResponse.Removed)
            return Task.FromResult(ClipDeleteStatus.NotFound);
        clip.Status = ClipStatusResponse.Removed;
        return Task.FromResult(ClipDeleteStatus.Ok);
    }

    public Task RecordViewAsync(string shortId)
    {
        var clip = Find(shortId);
        if (clip != null) clip.ViewCount++;
        return Task.CompletedTask;
    }

    public async Task<AdminClipListResponse> ListForAdminAsync(ClipStatusResponse? status, int page, int pageSize, PublicOrigin origin)
    {
        var all = _clips.Values.Where(c => status == null || c.Status == status).OrderByDescending(c => c.CreatedAt).ToList();
        var items = new List<AdminClipEntry>();
        foreach (var clip in all.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize))
        {
            items.Add(new AdminClipEntry
            {
                Clip = await ToResponseAsync(clip, origin, false),
                Attempts = clip.Attempts,
                Error = clip.Error,
                CreatedByPlayerId = Guid.Empty,
                CreatedByName = "You",
            });
        }
        return new AdminClipListResponse { Items = items, TotalCount = all.Count, Page = page, PageSize = pageSize };
    }

    public Task<bool> RetryAsync(string shortId)
    {
        var clip = Find(shortId);
        if (clip == null || clip.Status == ClipStatusResponse.Removed)
            return Task.FromResult(false);
        clip.Status = ClipStatusResponse.Pending;
        clip.Attempts = 0;
        clip.Error = null;
        return Task.FromResult(true);
    }

    private async Task<ClipResponse> ToResponseAsync(OfflineClip clip, PublicOrigin origin, bool includeReplay)
    {
        var replay = (await _replays.GetPublicReplayAsync(clip.Request.MatchId, clip.Request.DealNumber, clip.Locale)).Replay!;
        var ready = clip.Status == ClipStatusResponse.Ready;
        return new ClipResponse
        {
            ShortId = clip.ShortId,
            Status = clip.Status,
            MatchId = clip.Request.MatchId,
            DealNumber = clip.Request.DealNumber,
            FromTrick = clip.Request.FromTrick,
            ToTrick = clip.Request.ToTrick,
            ViewPosition = clip.View,
            ShowAllHands = clip.Request.ShowAllHands,
            Locale = clip.Locale,
            CreatedAt = clip.CreatedAt,
            RenderedAt = clip.RenderedAt,
            DurationMs = clip.DurationMs,
            ViewCount = clip.ViewCount,
            Title = ClipTexts.Title(replay.GameMode, clip.Request.DealNumber, clip.Request.FromTrick, clip.Request.ToTrick,
                Names(replay, CoreTeam.Team1), Names(replay, CoreTeam.Team2), clip.Locale),
            PageUrl = origin.Url($"/c/{clip.ShortId}"),
            PosterUrl = ready && clip.HasPoster ? origin.Url($"/c/{clip.ShortId}/poster.png") : null,
            VideoUrl = ready && clip.HasVideo ? origin.Url($"/c/{clip.ShortId}/video.mp4") : null,
            Replay = includeReplay ? replay : null,
        };
    }

    private static string Names(DealReplayResponse replay, CoreTeam team)
        => string.Join(" & ", replay.Players.Where(p => p.Team == team).Select(p => p.DisplayName));
}
