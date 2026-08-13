using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Players;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Integration tests for the match-end to new-game transition:
/// a full match is played by four humans (driven through the public
/// submit APIs), then the "Play Again" confirmation flow is exercised.
/// </summary>
public sealed class MatchEndTransitionTests
{
    private static readonly Guid CreatorUserId = Guid.NewGuid();
    private static readonly Guid Player2UserId = Guid.NewGuid();
    private static readonly Guid Player3UserId = Guid.NewGuid();
    private static readonly Guid Player4UserId = Guid.NewGuid();

    private sealed record TestContext(
        GameService GameService,
        RoomService RoomService,
        IRoomRepository RoomRepository,
        INotificationService Notifications);

    private static TestContext CreateServices(int? continueMatchWindowSeconds = null)
    {
        var roomRepository = new InMemoryRoomRepository();
        var gameRepository = new InMemoryGameRepository();
        var notifications = Substitute.For<INotificationService>();
        var aiRegistry = AiPlayerRegistry.CreateFromAssembly();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var configuration = Substitute.For<IConfiguration>();
        if (continueMatchWindowSeconds != null)
            configuration["Game:ContinueMatchWindowSeconds"].Returns(continueMatchWindowSeconds.Value.ToString());

        var gameService = new GameService(
            gameRepository, roomRepository, notifications, aiRegistry,
            serviceProvider, configuration,
            Substitute.For<ILogger<GameService>>(), Substitute.For<ILoggerFactory>());
        var roomService = new RoomService(
            roomRepository, gameService, notifications,
            Substitute.For<IChatService>(), aiRegistry, Substitute.For<ILogger<RoomService>>());
        serviceProvider.GetService(typeof(IRoomService)).Returns(roomService);

        return new TestContext(gameService, roomService, roomRepository, notifications);
    }

    private static (string RoomId, string GameId, GameSession Session) StartFourHumanGame(
        TestContext ctx, int turnTimerSeconds = 20)
    {
        var (create, createError) = ctx.RoomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Match End Test",
            CreatorName = "Player1",
            TurnTimerSeconds = turnTimerSeconds,
        }, "Player1", CreatorUserId);
        Assert.True(create != null, $"CreateRoom failed: {createError}");

        foreach (var (name, userId) in new[]
        {
            ("Player2", Player2UserId),
            ("Player3", Player3UserId),
            ("Player4", Player4UserId),
        })
        {
            var (join, joinError) = ctx.RoomService.JoinRoom(
                create.Room.RoomId, new JoinRoomRequest { DisplayName = name }, name, userId);
            Assert.True(join != null, $"JoinRoom failed for {name}: {joinError}");
        }

        var (start, startError) = ctx.RoomService.StartGame(create.Room.RoomId, CreatorUserId);
        Assert.True(start != null, $"StartGame failed: {startError}");

        var session = ctx.GameService.GetGame(start.GameId);
        Assert.NotNull(session);
        return (create.Room.RoomId, start.GameId, session);
    }

    /// <summary>
    /// Drives all four human players through the match by submitting a
    /// trivial valid action for every pending request, until the engine
    /// asks all of them to confirm ContinueMatch (i.e. the match ended).
    /// ContinueMatch itself is left pending for each test to resolve.
    /// </summary>
    private static async Task PlayUntilMatchEndAsync(GameService gameService, string gameId, GameSession session)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (true)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Match did not reach the ContinueMatch confirmation in time");

            var pendings = session.PendingActions.ToArray();

            if (pendings.Length == 4 && pendings.All(p => p.Value.ActionType == PendingActionType.ContinueMatch))
                return;

            foreach (var (position, pending) in pendings)
            {
                var clientId = session.ClientPositions.FirstOrDefault(kvp => kvp.Value == position).Key;
                if (clientId == null)
                    continue;

                // Submits are safe against races: if the pending action was
                // already resolved or replaced, the submit is simply rejected.
                switch (pending.ActionType)
                {
                    case PendingActionType.Cut:
                        gameService.SubmitCut(gameId, clientId, 16, fromTop: true);
                        break;

                    case PendingActionType.Negotiate:
                        if (pending.ValidNegotiationActions is { Count: > 0 } actions)
                        {
                            // Prefer Accept so negotiation terminates quickly
                            var action = actions.FirstOrDefault(a => a is AcceptAction) ?? actions[0];
                            gameService.SubmitNegotiation(gameId, clientId, action);
                        }
                        break;

                    case PendingActionType.PlayCard:
                        if (pending.ValidCards is { Count: > 0 } cards)
                            gameService.SubmitCardPlay(gameId, clientId, cards[0]);
                        break;

                    case PendingActionType.ContinueDeal:
                        gameService.SubmitContinueDeal(gameId, clientId);
                        break;

                    case PendingActionType.ContinueMatch:
                        break;
                }
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task MatchEnd_OnePlayerClicksPlayAgain_NewGameStartsForSameRoom()
    {
        var ctx = CreateServices(continueMatchWindowSeconds: 1);
        var (roomId, gameId, session) = StartFourHumanGame(ctx);

        await PlayUntilMatchEndAsync(ctx.GameService, gameId, session);

        // One player clicks Play Again; the other three let the window expire
        var clicker = session.ClientPositions.Keys.First();
        Assert.True(ctx.GameService.SubmitContinueMatch(gameId, clicker));

        await session.GameLoopTask!.WaitAsync(TimeSpan.FromSeconds(15));

        var room = ctx.RoomRepository.GetById(roomId);
        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Playing, room.Status);
        Assert.NotNull(room.GameSessionId);
        Assert.NotEqual(gameId, room.GameSessionId);
        await ctx.Notifications.Received(1).NotifyGameStartedAsync(roomId, room.GameSessionId!);
        await ctx.Notifications.DidNotReceive().NotifyRoomResetAsync(roomId);

        // Cleanup: stop the restarted game running in the background
        _ = ctx.GameService.TerminateGameAsync(room.GameSessionId!);
    }

    [Fact]
    public async Task MatchEnd_NobodyClicks_RoomResetsAndClientsAreNotified()
    {
        var ctx = CreateServices(continueMatchWindowSeconds: 1);
        var (roomId, gameId, session) = StartFourHumanGame(ctx);

        await PlayUntilMatchEndAsync(ctx.GameService, gameId, session);

        // Nobody clicks Play Again; the window expires
        await session.GameLoopTask!.WaitAsync(TimeSpan.FromSeconds(15));

        var room = ctx.RoomRepository.GetById(roomId);
        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Waiting, room.Status);
        Assert.Null(room.GameSessionId);
        await ctx.Notifications.Received(1).NotifyRoomResetAsync(roomId);
        await ctx.Notifications.DidNotReceive().NotifyGameStartedAsync(roomId, Arg.Is<string>(id => id != gameId));
    }

    [Fact]
    public async Task MatchEnd_LateClickAfterWindowClosed_IsRejected()
    {
        var ctx = CreateServices(continueMatchWindowSeconds: 1);
        var (_, gameId, session) = StartFourHumanGame(ctx);

        await PlayUntilMatchEndAsync(ctx.GameService, gameId, session);
        await session.GameLoopTask!.WaitAsync(TimeSpan.FromSeconds(15));

        // The window has closed: the submit must be rejected, not silently lost
        var clicker = session.ClientPositions.Keys.First();
        Assert.False(ctx.GameService.SubmitContinueMatch(gameId, clicker));
    }

    [Fact]
    public async Task MatchEnd_ContinueMatchWindow_IsIndependentOfTurnTimer()
    {
        var ctx = CreateServices();
        var (_, gameId, session) = StartFourHumanGame(ctx, turnTimerSeconds: 5);

        await PlayUntilMatchEndAsync(ctx.GameService, gameId, session);

        Assert.All(session.PendingActions.Values, pending =>
        {
            Assert.Equal(PendingActionType.ContinueMatch, pending.ActionType);
            Assert.Equal(WebApiPlayerAgent.DefaultContinueMatchTimeout, pending.TimeoutDuration);
        });

        _ = ctx.GameService.TerminateGameAsync(gameId);
    }

    [Fact]
    public async Task MatchEnd_AllPlayersClick_RestartsWithoutWaitingForWindow()
    {
        // Default 120s window: a prompt restart proves it does not wait for the timeout
        var ctx = CreateServices();
        var (roomId, gameId, session) = StartFourHumanGame(ctx);

        await PlayUntilMatchEndAsync(ctx.GameService, gameId, session);

        foreach (var clientId in session.ClientPositions.Keys.ToList())
            Assert.True(ctx.GameService.SubmitContinueMatch(gameId, clientId));

        await session.GameLoopTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(4, session.ContinueMatchConfirmed.Count);
        var room = ctx.RoomRepository.GetById(roomId);
        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Playing, room.Status);
        Assert.NotEqual(gameId, room.GameSessionId);
        await ctx.Notifications.DidNotReceive().NotifyRoomResetAsync(roomId);

        // Cleanup: stop the restarted game running in the background
        _ = ctx.GameService.TerminateGameAsync(room.GameSessionId!);
    }
}
