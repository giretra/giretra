using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Players;
using Giretra.Web.Services;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Tests for <see cref="WebApiPlayerAgent"/> pausing the game (instead of
/// playing timeout defaults) while its player is disconnected.
/// </summary>
public sealed class DisconnectPauseTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);

    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly GameSession _session;

    public DisconnectPauseTests()
    {
        _session = new GameSession
        {
            GameId = "game_test",
            RoomId = "room_test",
            ClientPositions = new Dictionary<string, PlayerPosition> { ["client1"] = PlayerPosition.Bottom },
            PlayerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>()
        };
    }

    private WebApiPlayerAgent CreateAgent(Func<string, bool> shouldPause)
    {
        return new WebApiPlayerAgent(
            PlayerPosition.Bottom,
            "client1",
            _session,
            _notifications,
            ShortTimeout,
            shouldPauseOnTimeout: shouldPause);
    }

    [Fact]
    public async Task Timeout_WhenNotPausing_PlaysDefault()
    {
        var agent = CreateAgent(_ => false);

        var result = await agent.ChooseCutAsync(32, null!).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal((16, true), result);
    }

    [Fact]
    public async Task Timeout_WhileDisconnected_DoesNotPlayDefault()
    {
        var agent = CreateAgent(_ => true);

        var task = agent.ChooseCutAsync(32, null!);
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(task.IsCompleted);

        // Cleanup: cancel the game so the paused wait ends
        _session.CancellationTokenSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Pause_ActionSubmittedDuringPause_ReturnsSubmittedAction()
    {
        var agent = CreateAgent(_ => true);

        var task = agent.ChooseCutAsync(32, null!);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(task.IsCompleted);

        // Player rejoined and submitted their cut while the game was paused
        var pending = Assert.Contains(PlayerPosition.Bottom, (IDictionary<PlayerPosition, PendingAction>)_session.PendingActions);
        pending.CutTcs!.TrySetResult((5, false));

        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((5, false), result);
    }

    [Fact]
    public async Task Pause_OnReconnect_RestartsTimerAndRenotifies()
    {
        var connected = false;
        // ReSharper disable once AccessToModifiedClosure
        var agent = CreateAgent(_ => !connected);

        var task = agent.ChooseCutAsync(32, null!);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(task.IsCompleted);

        // Reconnect without acting: the timer restarts, then times out normally
        connected = true;
        var result = await task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal((16, true), result);
        // Initial turn notification plus at least one re-notification on resume
        await _notifications.Received(2).NotifyYourTurnAsync(
            _session.GameId, "client1", PlayerPosition.Bottom, PendingActionType.Cut, Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Pause_GameCancelled_StopsWaiting()
    {
        var agent = CreateAgent(_ => true);

        var task = agent.ChooseCutAsync(32, null!);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        _session.CancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
