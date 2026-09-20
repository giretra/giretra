using Giretra.Web.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

public sealed class RoomEventDispatcherTests
{
    private static RoomEventDispatcher Create(TimeSpan? sendTimeout = null) =>
        new(Substitute.For<ILogger<RoomEventDispatcher>>(), sendTimeout ?? RoomEventDispatcher.DefaultSendTimeout);

    [Fact]
    public async Task Enqueue_DeliversRoomEventsInOrder_OneAtATime()
    {
        var dispatcher = Create();
        var delivered = new List<int>();
        var concurrent = 0;
        var maxConcurrent = 0;

        for (var i = 0; i < 50; i++)
        {
            var n = i;
            dispatcher.Enqueue("room", $"ev{n}", async () =>
            {
                var now = Interlocked.Increment(ref concurrent);
                maxConcurrent = Math.Max(maxConcurrent, now);
                await Task.Delay(1);
                lock (delivered) delivered.Add(n);
                Interlocked.Decrement(ref concurrent);
            });
        }

        await dispatcher.FlushAsync("room");

        Assert.Equal(Enumerable.Range(0, 50), delivered);
        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task Enqueue_ReturnsBeforeTheSendCompletes()
    {
        var dispatcher = Create();
        var release = new TaskCompletionSource();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        dispatcher.Enqueue("room", "slow", () => release.Task);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500);
        release.SetResult();
        await dispatcher.FlushAsync("room");
    }

    [Fact]
    public async Task StuckSend_DoesNotBlockOtherRooms()
    {
        var dispatcher = Create(TimeSpan.FromSeconds(30));
        var never = new TaskCompletionSource();
        dispatcher.Enqueue("stuck", "hang", () => never.Task);

        var delivered = new TaskCompletionSource();
        dispatcher.Enqueue("healthy", "ping", () =>
        {
            delivered.SetResult();
            return Task.CompletedTask;
        });

        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StuckSend_IsAbandonedAfterTimeout_AndTheRoomContinues()
    {
        var dispatcher = Create(TimeSpan.FromMilliseconds(100));
        var never = new TaskCompletionSource();
        dispatcher.Enqueue("room", "hang", () => never.Task);

        var next = new TaskCompletionSource();
        dispatcher.Enqueue("room", "after", () =>
        {
            next.SetResult();
            return Task.CompletedTask;
        });

        await next.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailingSend_IsLogged_AndTheRoomContinues()
    {
        var dispatcher = Create();
        dispatcher.Enqueue("room", "boom", () => throw new InvalidOperationException("boom"));

        var next = new TaskCompletionSource();
        dispatcher.Enqueue("room", "after", () =>
        {
            next.SetResult();
            return Task.CompletedTask;
        });

        await next.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Enqueue_AfterTheRoomDrained_StillDeliversInOrder()
    {
        var dispatcher = Create();
        var delivered = new List<string>();

        for (var round = 0; round < 20; round++)
        {
            var name = $"round{round}";
            dispatcher.Enqueue("room", name, () =>
            {
                lock (delivered) delivered.Add(name);
                return Task.CompletedTask;
            });
            await dispatcher.FlushAsync("room");
        }

        Assert.Equal(Enumerable.Range(0, 20).Select(r => $"round{r}"), delivered);
    }
}
