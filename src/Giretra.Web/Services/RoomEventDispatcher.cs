using System.Collections.Concurrent;

namespace Giretra.Web.Services;

/// <summary>
/// Per-room outbox for real-time sends. The game loop enqueues an event and
/// carries on; a single drain task per room delivers the events in order, so a
/// client that stopped reading its socket (phone with the screen off, tab in the
/// background) can no longer stall the table. A send that does not complete
/// within <see cref="DefaultSendTimeout"/> is abandoned (the message stays
/// buffered for that connection; the hub's client timeout eventually drops it).
/// </summary>
public sealed class RoomEventDispatcher
{
    public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<string, Outbox> _outboxes = new();
    private readonly ILogger<RoomEventDispatcher> _logger;
    private readonly TimeSpan _sendTimeout;

    public RoomEventDispatcher(ILogger<RoomEventDispatcher> logger)
        : this(logger, DefaultSendTimeout)
    {
    }

    public RoomEventDispatcher(ILogger<RoomEventDispatcher> logger, TimeSpan sendTimeout)
    {
        _logger = logger;
        _sendTimeout = sendTimeout;
    }

    /// <summary>
    /// Queues a send for the room. Returns immediately; sends for one room run
    /// one at a time in the order they were queued.
    /// </summary>
    public void Enqueue(string roomId, string eventName, Func<Task> send)
    {
        while (true)
        {
            var outbox = _outboxes.GetOrAdd(roomId, static id => new Outbox(id));
            bool startDrain;
            lock (outbox)
            {
                // The drain task retires an empty outbox under its lock; an
                // enqueue that raced with that retirement starts over.
                if (outbox.Retired)
                    continue;

                outbox.Items.Enqueue((eventName, send));
                startDrain = !outbox.Draining;
                outbox.Draining = true;
            }

            if (startDrain)
                _ = Task.Run(() => DrainAsync(outbox));
            return;
        }
    }

    /// <summary>
    /// Completes once every send queued for the room before this call has been
    /// attempted.
    /// </summary>
    public Task FlushAsync(string roomId)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(roomId, "Flush", () =>
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        });
        return tcs.Task;
    }

    private async Task DrainAsync(Outbox outbox)
    {
        while (true)
        {
            (string EventName, Func<Task> Send) item;
            lock (outbox)
            {
                if (outbox.Items.Count == 0)
                {
                    outbox.Draining = false;
                    outbox.Retired = true;
                    _outboxes.TryRemove(new KeyValuePair<string, Outbox>(outbox.RoomId, outbox));
                    return;
                }
                item = outbox.Items.Dequeue();
            }

            await SendAsync(outbox.RoomId, item.EventName, item.Send);
        }
    }

    private async Task SendAsync(string roomId, string eventName, Func<Task> send)
    {
        try
        {
            await send().WaitAsync(_sendTimeout);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Delivery of {EventName} to room {RoomId} did not complete within {Timeout}; moving on to the next event",
                eventName, roomId, _sendTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delivery of {EventName} to room {RoomId} failed", eventName, roomId);
        }
    }

    private sealed class Outbox(string roomId)
    {
        public string RoomId { get; } = roomId;
        public Queue<(string EventName, Func<Task> Send)> Items { get; } = new();
        public bool Draining { get; set; }
        public bool Retired { get; set; }
    }
}
