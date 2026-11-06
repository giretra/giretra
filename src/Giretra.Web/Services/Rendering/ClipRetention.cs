using Giretra.Model;
using Giretra.Model.Enums;
using Giretra.Web.Services.Clips;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Rendering;

/// <summary>Frees the files of clips nobody opened for a long time; they are re-rendered on demand.</summary>
public interface IClipRetention
{
    /// <summary>Evicts ready clips last viewed (or rendered) before <paramref name="notViewedSince"/>; returns the count.</summary>
    Task<int> EvictAsync(DateTimeOffset notViewedSince, CancellationToken ct);
}

public sealed class PostgresClipRetention : IClipRetention
{
    private const int BatchSize = 500;

    private readonly GiretraDbContext _db;
    private readonly ClipStorage _storage;
    private readonly ILogger<PostgresClipRetention> _logger;

    public PostgresClipRetention(GiretraDbContext db, ClipStorage storage, ILogger<PostgresClipRetention> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<int> EvictAsync(DateTimeOffset notViewedSince, CancellationToken ct)
    {
        var stale = await _db.Clips
            .Where(c => c.Status == ClipStatus.Ready && (c.LastViewedAt ?? c.RenderedAt ?? c.CreatedAt) < notViewedSince)
            .OrderBy(c => c.LastViewedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var clip in stale)
        {
            try
            {
                _storage.DeleteFiles(clip.ShortId);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete files of clip {ShortId}; evicting anyway", clip.ShortId);
            }
            clip.Status = ClipStatus.Evicted;
            clip.HasPoster = false;
            clip.HasVideo = false;
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync(ct);
        return stale.Count;
    }
}

/// <summary>Runs the eviction sweep periodically in the render worker.</summary>
public sealed class ClipRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ClipRenderOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ClipRetentionHostedService> _logger;

    public ClipRetentionHostedService(
        IServiceScopeFactory scopes,
        IOptions<ClipRenderOptions> options,
        ILogger<ClipRetentionHostedService> logger,
        TimeProvider? time = null)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _options.RetentionDays <= 0)
            return;

        var period = TimeSpan.FromHours(Math.Max(1, _options.RetentionSweepHours));
        // Let the queue loop and the database come up first.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<IClipRetention>();
                var cutoff = _time.GetUtcNow() - TimeSpan.FromDays(_options.RetentionDays);
                var evicted = await retention.EvictAsync(cutoff, stoppingToken);
                if (evicted > 0)
                    _logger.LogInformation("Evicted {Count} clips not viewed since {Cutoff:u}", evicted, cutoff);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Clip retention sweep failed");
            }

            await Task.Delay(period, stoppingToken);
        }
    }
}
