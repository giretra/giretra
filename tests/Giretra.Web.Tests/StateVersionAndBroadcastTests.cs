using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Players;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Tests for the state version exposed to clients and for room-wide events
/// being broadcast once per game rather than once per human agent.
/// </summary>
public sealed class StateVersionAndBroadcastTests
{
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private static GameSession CreateSession() => new()
    {
        GameId = "game_test",
        RoomId = "room_test",
        ClientPositions = new Dictionary<string, PlayerPosition> { ["client1"] = PlayerPosition.Bottom },
        PlayerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>()
    };

    private WebApiPlayerAgent CreateAgent(GameSession session, PlayerPosition position, bool broadcasts, TimeSpan? timeout = null) =>
        new(position, $"client_{position}", session, _notifications,
            timeout ?? TimeSpan.FromMilliseconds(50), broadcastsRoomEvents: broadcasts);

    [Fact]
    public async Task EngineCallback_BumpsStateVersion_OnEveryAgent()
    {
        var session = CreateSession();
        var broadcaster = CreateAgent(session, PlayerPosition.Bottom, broadcasts: true);
        var silent = CreateAgent(session, PlayerPosition.Top, broadcasts: false);
        var card = new Card(CardRank.Ace, CardSuit.Hearts);

        var before = session.StateVersion;
        await broadcaster.OnCardPlayedAsync(PlayerPosition.Left, card, null!, null!);
        var afterFirst = session.StateVersion;
        await silent.OnCardPlayedAsync(PlayerPosition.Left, card, null!, null!);

        Assert.True(afterFirst > before);
        Assert.True(session.StateVersion > afterFirst);
    }

    [Fact]
    public async Task PendingAction_BumpsStateVersion_WhenSetAndWhenCleared()
    {
        var session = CreateSession();
        var agent = CreateAgent(session, PlayerPosition.Bottom, broadcasts: true, timeout: TimeSpan.FromMinutes(1));

        var before = session.StateVersion;
        var task = agent.ChooseCutAsync(32, null!);
        var pending = Assert.Contains(PlayerPosition.Bottom, (IDictionary<PlayerPosition, PendingAction>)session.PendingActions);
        var whilePending = session.StateVersion;
        pending.CutTcs!.TrySetResult((5, false));
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(whilePending > before);
        Assert.True(session.StateVersion > whilePending);
    }

    [Fact]
    public async Task RoomWideCallbacks_AreOnlyRelayedByTheBroadcastingAgent()
    {
        var session = CreateSession();
        var broadcaster = CreateAgent(session, PlayerPosition.Bottom, broadcasts: true);
        var silent = CreateAgent(session, PlayerPosition.Top, broadcasts: false);
        var card = new Card(CardRank.Ace, CardSuit.Hearts);

        await broadcaster.OnCardPlayedAsync(PlayerPosition.Left, card, null!, null!);
        await silent.OnCardPlayedAsync(PlayerPosition.Left, card, null!, null!);
        await broadcaster.OnDealStartedAsync(null!);
        await silent.OnDealStartedAsync(null!);
        await broadcaster.OnTrickCompletedAsync(null!, PlayerPosition.Left, null!, null!);
        await silent.OnTrickCompletedAsync(null!, PlayerPosition.Left, null!, null!);

        await _notifications.Received(1).NotifyCardPlayedAsync(session.GameId, PlayerPosition.Left, card, Arg.Any<HandState>(), Arg.Any<MatchState>());
        await _notifications.Received(1).NotifyDealStartedAsync(session.GameId, Arg.Any<MatchState>());
        await _notifications.Received(1).NotifyTrickCompletedAsync(session.GameId, Arg.Any<TrickState>(), PlayerPosition.Left, Arg.Any<HandState>(), Arg.Any<MatchState>());
    }

    [Fact]
    public async Task NonBroadcastingAgent_StillNotifiesItsOwnTurn()
    {
        var session = CreateSession();
        var agent = CreateAgent(session, PlayerPosition.Top, broadcasts: false);

        await agent.ChooseCutAsync(32, null!).WaitAsync(TimeSpan.FromSeconds(5));

        await _notifications.Received(1).NotifyYourTurnAsync(
            session.GameId, agent.ClientId, PlayerPosition.Top, PendingActionType.Cut, Arg.Any<DateTime>());
    }

    [Fact]
    public async Task CreateGame_WithSeveralHumans_BroadcastsEachRoomEventOnce()
    {
        var gameRepository = new InMemoryGameRepository();
        var gameService = new GameService(
            gameRepository,
            new InMemoryRoomRepository(),
            _notifications,
            AiPlayerRegistry.CreateFromAssembly(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<GameService>>(),
            Substitute.For<ILoggerFactory>());
        var room = CreateRoomWithHumans(PlayerPosition.Bottom, PlayerPosition.Top, PlayerPosition.Left);

        var session = gameService.CreateGame(room)!;
        var card = new Card(CardRank.Ace, CardSuit.Hearts);
        foreach (var position in room.PlayerSlots.Where(kv => kv.Value != null).Select(kv => kv.Key))
        {
            await session.PlayerAgents[position].OnCardPlayedAsync(PlayerPosition.Right, card, null!, null!);
        }

        await _notifications.Received(1).NotifyCardPlayedAsync(session.GameId, PlayerPosition.Right, card, Arg.Any<HandState>(), Arg.Any<MatchState>());
    }

    [Fact]
    public void GetGameState_ExposesSessionStateVersion()
    {
        var gameRepository = new InMemoryGameRepository();
        var gameService = new GameService(
            gameRepository,
            new InMemoryRoomRepository(),
            _notifications,
            AiPlayerRegistry.CreateFromAssembly(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<IConfiguration>(),
            Substitute.For<ILogger<GameService>>(),
            Substitute.For<ILoggerFactory>());
        var session = gameService.CreateGame(CreateRoomWithHumans(PlayerPosition.Bottom))!;
        // CreateGame starts the game loop, which keeps bumping the version in the
        // background, so assert monotonicity around the calls rather than equality.
        session.BumpStateVersion();
        var lowerBound = session.StateVersion;

        var state = gameService.GetGameState(session.GameId);
        var playerState = gameService.GetPlayerState(session.GameId, "client_Bottom");
        var upperBound = session.StateVersion;

        Assert.NotNull(state);
        Assert.NotNull(playerState);
        Assert.InRange(state.StateVersion, lowerBound, upperBound);
        Assert.InRange(playerState.GameState.StateVersion, state.StateVersion, upperBound);
    }

    private static Room CreateRoomWithHumans(params PlayerPosition[] positions)
    {
        var room = new Room
        {
            RoomId = "room_test",
            Name = "Test Room",
            CreatorClientId = $"client_{positions[0]}",
            OwnerUserId = Guid.NewGuid()
        };
        foreach (var position in positions)
        {
            room.PlayerSlots[position] = new ConnectedClient
            {
                ClientId = $"client_{position}",
                DisplayName = position.ToString(),
                IsPlayer = true,
                Position = position
            };
        }
        return room;
    }
}
