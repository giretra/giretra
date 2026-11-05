using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Clips;

/// <summary>Per-player cap on new clips per rolling 24 hours. In-memory, single web instance.</summary>
public sealed class ClipThrottle
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly int _maxPerWindow;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, List<DateTimeOffset>> _history = new();

    public ClipThrottle(IOptions<ClipOptions> options, TimeProvider? time = null)
    {
        _maxPerWindow = options.Value.MaxPerDay;
        _time = time ?? TimeProvider.System;
    }

    public bool TryAcquire(Guid playerId)
    {
        var now = _time.GetUtcNow();
        var timestamps = _history.GetOrAdd(playerId, _ => []);
        lock (timestamps)
        {
            timestamps.RemoveAll(t => now - t > Window);
            if (timestamps.Count >= _maxPerWindow)
                return false;
            timestamps.Add(now);
            return true;
        }
    }
}
