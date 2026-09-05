using System.Collections.Concurrent;

namespace Giretra.Web.Services;

/// <summary>
/// Per-user rate limit for the contact form: a short pause between two messages and a
/// cap per hour. In-memory, which is enough for a single web instance.
/// </summary>
public sealed class FeedbackThrottle
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);
    public const int MaxPerWindow = 5;

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, List<DateTimeOffset>> _history = new();

    public FeedbackThrottle(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Records an attempt and reports whether it is allowed.
    /// </summary>
    public bool TryAcquire(Guid userId)
    {
        var now = _time.GetUtcNow();
        var timestamps = _history.GetOrAdd(userId, _ => []);

        lock (timestamps)
        {
            timestamps.RemoveAll(t => now - t > Window);

            if (timestamps.Count >= MaxPerWindow)
                return false;
            if (timestamps.Count > 0 && now - timestamps[^1] < MinInterval)
                return false;

            timestamps.Add(now);
            return true;
        }
    }
}
