using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Services.Clips;
using Giretra.Web.Services.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Tests.Services.Rendering;

public sealed class ClipRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);

    private readonly GiretraDbContext _db;
    private readonly ClipStorage _storage;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "giretra-retention-tests", Guid.NewGuid().ToString("N"));

    public ClipRetentionTests()
    {
        _db = new GiretraDbContext(new DbContextOptionsBuilder<GiretraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _storage = new ClipStorage(Options.Create(new ClipOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Evict_FreesFilesOfReadyClipsNotViewedSinceCutoff_Only()
    {
        var old = AddClip("oldclip001", ClipStatus.Ready, rendered: Now.AddDays(-300), lastViewed: Now.AddDays(-200));
        var neverViewedOld = AddClip("oldclip002", ClipStatus.Ready, rendered: Now.AddDays(-181), lastViewed: null);
        var recentlyViewed = AddClip("newclip001", ClipStatus.Ready, rendered: Now.AddDays(-300), lastViewed: Now.AddDays(-1));
        var failed = AddClip("failclip01", ClipStatus.Failed, rendered: null, lastViewed: null);
        await _db.SaveChangesAsync();
        foreach (var c in new[] { old, neverViewedOld, recentlyViewed })
            WriteFiles(c.ShortId);

        var retention = new PostgresClipRetention(_db, _storage, NullLogger<PostgresClipRetention>.Instance);
        var count = await retention.EvictAsync(Now.AddDays(-180), CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(ClipStatus.Evicted, old.Status);
        Assert.False(old.HasPoster);
        Assert.False(old.HasVideo);
        Assert.False(File.Exists(_storage.VideoPath(old.ShortId)));
        Assert.Equal(ClipStatus.Evicted, neverViewedOld.Status);
        Assert.Equal(ClipStatus.Ready, recentlyViewed.Status);
        Assert.True(File.Exists(_storage.VideoPath(recentlyViewed.ShortId)));
        Assert.Equal(ClipStatus.Failed, failed.Status);
    }

    private Clip AddClip(string shortId, ClipStatus status, DateTimeOffset? rendered, DateTimeOffset? lastViewed)
    {
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            ShortId = shortId,
            MatchId = Guid.NewGuid(),
            DealNumber = 1,
            FromTrick = 1,
            ToTrick = 8,
            Status = status,
            CreatedByPlayerId = Guid.NewGuid(),
            CreatedAt = Now.AddDays(-400),
            RenderedAt = rendered,
            LastViewedAt = lastViewed,
            HasPoster = status == ClipStatus.Ready,
            HasVideo = status == ClipStatus.Ready,
        };
        _db.Clips.Add(clip);
        return clip;
    }

    private void WriteFiles(string shortId)
    {
        _storage.EnsureDirectoryFor(shortId);
        File.WriteAllBytes(_storage.PosterPath(shortId), [1]);
        File.WriteAllBytes(_storage.VideoPath(shortId), [1]);
    }
}
