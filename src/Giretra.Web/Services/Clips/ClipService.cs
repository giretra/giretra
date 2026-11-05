using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;
using Microsoft.EntityFrameworkCore;
using CoreGameMode = Giretra.Core.GameModes.GameMode;
using CorePosition = Giretra.Core.Players.PlayerPosition;
using CoreTeam = Giretra.Core.Players.Team;

namespace Giretra.Web.Services.Clips;

public sealed class ClipService : IClipService
{
    private const int TricksPerDeal = 8;

    private readonly GiretraDbContext _db;
    private readonly IDealReplayService _replays;
    private readonly ClipThrottle _throttle;
    private readonly ClipStorage _storage;
    private readonly TimeProvider _time;
    private readonly ILogger<ClipService> _logger;

    public ClipService(
        GiretraDbContext db,
        IDealReplayService replays,
        ClipThrottle throttle,
        ClipStorage storage,
        ILogger<ClipService> logger,
        TimeProvider? time = null)
    {
        _db = db;
        _replays = replays;
        _throttle = throttle;
        _storage = storage;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ClipCreateResult> CreateAsync(Guid userId, CreateClipRequest request, PublicOrigin origin)
    {
        if (request.FromTrick < 1 || request.ToTrick > TricksPerDeal || request.FromTrick > request.ToTrick)
            return new ClipCreateResult(ClipCreateStatus.InvalidRequest, null, "Trick range must be within 1..8.");

        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
            return new ClipCreateResult(ClipCreateStatus.Forbidden, null);

        // The participant replay does every match/deal validation we need.
        var replay = await _replays.GetReplayAsync(request.MatchId, request.DealNumber, userId);
        switch (replay.Status)
        {
            case DealReplayStatus.MatchNotFound:
            case DealReplayStatus.DealNotFound:
                return new ClipCreateResult(ClipCreateStatus.MatchNotFound, null);
            case DealReplayStatus.Forbidden:
                return new ClipCreateResult(ClipCreateStatus.Forbidden, null);
            case DealReplayStatus.NotReplayable:
                return new ClipCreateResult(ClipCreateStatus.NotReplayable, null);
        }

        var replayData = replay.Replay!;
        var mySeat = replayData.Players.FirstOrDefault(p => p.PlayerId == player.Id)?.Position ?? CorePosition.Bottom;
        var view = request.ViewPosition ?? mySeat;
        var locale = ClipTexts.Normalize(request.Locale);

        var existing = await FindExistingAsync(request, view, locale);
        if (existing != null)
            return new ClipCreateResult(ClipCreateStatus.Existing, await ToResponseAsync(existing, origin, includeReplay: false));

        if (!_throttle.TryAcquire(player.Id))
            return new ClipCreateResult(ClipCreateStatus.Throttled, null, "Too many clips today. Try again tomorrow.");

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            ShortId = ShortId.Generate(),
            MatchId = request.MatchId,
            DealNumber = (short)request.DealNumber,
            FromTrick = (short)request.FromTrick,
            ToTrick = (short)request.ToTrick,
            ViewPosition = (PlayerPosition)(int)view,
            ShowAllHands = request.ShowAllHands,
            Locale = locale,
            Status = ClipStatus.Pending,
            CreatedByPlayerId = player.Id,
            CreatedAt = _time.GetUtcNow(),
        };
        _db.Clips.Add(clip);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Lost a race on the dedupe index (or, far less likely, the short id).
            _logger.LogInformation(ex, "Clip insert collided, returning the existing clip");
            _db.Entry(clip).State = EntityState.Detached;
            existing = await FindExistingAsync(request, view, locale);
            if (existing != null)
                return new ClipCreateResult(ClipCreateStatus.Existing, await ToResponseAsync(existing, origin, includeReplay: false));
            throw;
        }

        return new ClipCreateResult(ClipCreateStatus.Created, await ToResponseAsync(clip, origin, includeReplay: false));
    }

    public async Task<ClipResponse?> GetPublicAsync(string shortId, PublicOrigin origin, bool includeReplay = true)
    {
        if (!ShortId.IsValid(shortId))
            return null;
        var clip = await _db.Clips.AsNoTracking().FirstOrDefaultAsync(c => c.ShortId == shortId);
        return clip == null ? null : await ToResponseAsync(clip, origin, includeReplay && clip.Status != ClipStatus.Removed);
    }

    public async Task<ClipListResponse> ListMineAsync(Guid userId, PublicOrigin origin)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
            return new ClipListResponse { Items = [] };

        var clips = await _db.Clips.AsNoTracking()
            .Where(c => c.CreatedByPlayerId == player.Id && c.Status != ClipStatus.Removed)
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync();

        var items = new List<ClipResponse>(clips.Count);
        foreach (var clip in clips)
            items.Add(await ToResponseAsync(clip, origin, includeReplay: false));
        return new ClipListResponse { Items = items };
    }

    public async Task<ClipDeleteStatus> DeleteAsync(Guid userId, string shortId, bool isModerator)
    {
        var clip = await _db.Clips.FirstOrDefaultAsync(c => c.ShortId == shortId);
        if (clip == null || clip.Status == ClipStatus.Removed)
            return ClipDeleteStatus.NotFound;

        var player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == userId);
        if (!isModerator && (player == null || clip.CreatedByPlayerId != player.Id))
            return ClipDeleteStatus.Forbidden;

        clip.Status = ClipStatus.Removed;
        clip.HasPoster = false;
        clip.HasVideo = false;
        await _db.SaveChangesAsync();
        try
        {
            _storage.DeleteFiles(shortId);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete files of clip {ShortId}", shortId);
        }
        return ClipDeleteStatus.Ok;
    }

    public async Task RecordViewAsync(string shortId)
    {
        var now = _time.GetUtcNow();
        await _db.Clips
            .Where(c => c.ShortId == shortId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ViewCount, c => c.ViewCount + 1)
                .SetProperty(c => c.LastViewedAt, now));
    }

    private Task<Clip?> FindExistingAsync(CreateClipRequest request, CorePosition view, string locale)
    {
        var position = (PlayerPosition)(int)view;
        return _db.Clips.FirstOrDefaultAsync(c =>
            c.MatchId == request.MatchId
            && c.DealNumber == request.DealNumber
            && c.FromTrick == request.FromTrick
            && c.ToTrick == request.ToTrick
            && c.ViewPosition == position
            && c.ShowAllHands == request.ShowAllHands
            && c.Locale == locale);
    }

    private async Task<ClipResponse> ToResponseAsync(Clip clip, PublicOrigin origin, bool includeReplay)
    {
        // The public replay (names filtered by privacy) also gives us the title's ingredients.
        var replay = clip.Status == ClipStatus.Removed
            ? null
            : (await _replays.GetPublicReplayAsync(clip.MatchId, clip.DealNumber, clip.Locale)).Replay;

        var title = replay == null
            ? ClipTexts.Title(CoreGameMode.AllTrumps, clip.DealNumber, clip.FromTrick, clip.ToTrick, "?", "?", clip.Locale)
            : ClipTexts.Title(
                replay.GameMode, clip.DealNumber, clip.FromTrick, clip.ToTrick,
                TeamNames(replay, CoreTeam.Team1), TeamNames(replay, CoreTeam.Team2), clip.Locale);

        var ready = clip.Status == ClipStatus.Ready;
        return new ClipResponse
        {
            ShortId = clip.ShortId,
            Status = (ClipStatusResponse)(int)clip.Status,
            MatchId = clip.MatchId,
            DealNumber = clip.DealNumber,
            FromTrick = clip.FromTrick,
            ToTrick = clip.ToTrick,
            ViewPosition = (CorePosition)(int)clip.ViewPosition,
            ShowAllHands = clip.ShowAllHands,
            Locale = clip.Locale,
            CreatedAt = clip.CreatedAt,
            RenderedAt = clip.RenderedAt,
            DurationMs = clip.DurationMs,
            ViewCount = clip.ViewCount,
            Title = title,
            PageUrl = origin.Url($"/c/{clip.ShortId}"),
            PosterUrl = ready && clip.HasPoster ? origin.Url($"/c/{clip.ShortId}/poster.png") : null,
            VideoUrl = ready && clip.HasVideo ? origin.Url($"/c/{clip.ShortId}/video.mp4") : null,
            Replay = includeReplay ? replay : null,
        };
    }

    private static string TeamNames(DealReplayResponse replay, CoreTeam team)
        => string.Join(" & ", replay.Players.Where(p => p.Team == team).Select(p => p.DisplayName));
}
