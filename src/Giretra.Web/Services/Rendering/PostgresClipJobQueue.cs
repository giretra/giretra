using System.Data;
using System.Data.Common;
using Giretra.Model;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// Job queue on the <c>clips</c> table: a single <c>UPDATE … WHERE id = (SELECT … FOR UPDATE
/// SKIP LOCKED) RETURNING id</c> claims one row atomically, so several workers can coexist.
/// </summary>
public sealed class PostgresClipJobQueue : IClipJobQueue
{
    private readonly GiretraDbContext _db;
    private readonly ClipRenderOptions _options;
    private readonly TimeProvider _time;

    public PostgresClipJobQueue(GiretraDbContext db, IOptions<ClipRenderOptions> options, TimeProvider? time = null)
    {
        _db = db;
        _options = options.Value;
        _time = time ?? TimeProvider.System;
    }

    public async Task<ClipJob?> ClaimAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var staleBefore = now - TimeSpan.FromSeconds(_options.JobTimeoutSeconds * 2);

        // Claims older than twice the job timeout belong to a dead worker.
        await _db.Database.ExecuteSqlAsync($"""
            UPDATE clips
            SET status = CASE WHEN attempts >= {_options.MaxAttempts} THEN {(int)ClipStatus.Failed} ELSE {(int)ClipStatus.Pending} END,
                error = CASE WHEN attempts >= {_options.MaxAttempts} THEN 'Render timed out' ELSE error END
            WHERE status = {(int)ClipStatus.Rendering} AND claimed_at < {staleBefore}
            """, ct);

        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE clips
            SET status = @rendering, attempts = attempts + 1, claimed_at = @now
            WHERE id = (
                SELECT id FROM clips
                WHERE status = @pending
                ORDER BY created_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING id
            """;
        AddParameter(command, "rendering", (int)ClipStatus.Rendering);
        AddParameter(command, "pending", (int)ClipStatus.Pending);
        AddParameter(command, "now", now);

        var claimed = await command.ExecuteScalarAsync(ct);
        if (claimed is not Guid id)
            return null;

        var clip = await _db.Clips.AsNoTracking().FirstAsync(c => c.Id == id, ct);
        return new ClipJob(clip.Id, clip.ShortId, clip.Locale, clip.Attempts);
    }

    public async Task CompleteAsync(ClipJob job, ClipRenderOutput output, CancellationToken ct)
    {
        var clip = await _db.Clips.FirstAsync(c => c.Id == job.Id, ct);
        if (clip.Status == ClipStatus.Removed)
            return;
        clip.Status = ClipStatus.Ready;
        clip.HasPoster = output.HasPoster;
        clip.HasVideo = output.HasVideo;
        clip.DurationMs = output.DurationMs;
        clip.RenderedAt = _time.GetUtcNow();
        clip.Error = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task FailAsync(ClipJob job, string error, CancellationToken ct)
    {
        var clip = await _db.Clips.FirstAsync(c => c.Id == job.Id, ct);
        if (clip.Status == ClipStatus.Removed)
            return;
        clip.Error = error.Length > 1000 ? error[..1000] : error;
        clip.Status = clip.Attempts >= _options.MaxAttempts ? ClipStatus.Failed : ClipStatus.Pending;
        await _db.SaveChangesAsync(ct);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
