using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Integration tests for <see cref="IGameService"/>.
/// </summary>
public sealed class GameServiceTests
{
    private readonly IGameRepository _gameRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly INotificationService _notifications;
    private readonly ILogger<GameService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _gameRepository = new InMemoryGameRepository();
        _roomRepository = new InMemoryRoomRepository();
        _notifications = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<GameService>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        var aiRegistry = AiPlayerRegistry.CreateFromAssembly();
        _serviceProvider = Substitute.For<IServiceProvider>();
        var configuration = Substitute.For<IConfiguration>();
        _gameService = new GameService(_gameRepository, _roomRepository, _notifications, aiRegistry, _serviceProvider, configuration, _logger, _loggerFactory);
    }

    #region CreateGame Tests

    [Fact]
    public void CreateGame_WithValidRoom_CreatesGameSession()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.NotNull(session);
        Assert.StartsWith("game_", session.GameId);
        Assert.Equal(room.RoomId, session.RoomId);
    }

    [Fact]
    public void CreateGame_StoresSessionInRepository()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        var stored = _gameRepository.GetById(session!.GameId);
        Assert.NotNull(stored);
        Assert.Equal(session.GameId, stored.GameId);
    }

    [Fact]
    public void CreateGame_CreatesPlayerAgentsForAllPositions()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.Equal(4, session!.PlayerAgents.Count);
        Assert.Contains(PlayerPosition.Bottom, session.PlayerAgents.Keys);
        Assert.Contains(PlayerPosition.Left, session.PlayerAgents.Keys);
        Assert.Contains(PlayerPosition.Top, session.PlayerAgents.Keys);
        Assert.Contains(PlayerPosition.Right, session.PlayerAgents.Keys);
    }

    [Fact]
    public void CreateGame_MapsClientPositionsCorrectly()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var client = room.PlayerSlots[PlayerPosition.Bottom]!;

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.Single(session!.ClientPositions);
        Assert.Equal(PlayerPosition.Bottom, session.GetPositionForClient(client.ClientId));
    }

    [Fact]
    public void CreateGame_WithMultipleHumanPlayers_MapsAllClients()
    {
        // Arrange
        var room = CreateTestRoomWithMultipleHumans();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.Equal(2, session!.ClientPositions.Count);
    }

    [Fact]
    public void CreateGame_StartsGameLoopTask()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.NotNull(session!.GameLoopTask);
    }

    [Fact]
    public void CreateGame_InitializesGameManager()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();

        // Act
        var session = _gameService.CreateGame(room);

        // Assert
        Assert.NotNull(session!.GameManager);
    }

    #endregion

    #region GetGame Tests

    [Fact]
    public void GetGame_WithValidId_ReturnsSession()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room);

        // Act
        var retrieved = _gameService.GetGame(session!.GameId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.GameId, retrieved.GameId);
    }

    [Fact]
    public void GetGame_WithInvalidId_ReturnsNull()
    {
        // Act
        var retrieved = _gameService.GetGame("nonexistent");

        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region GetGameState Tests

    [Fact]
    public async Task GetGameState_ReturnsCurrentState()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room);

        // Wait briefly for game to initialize
        await Task.Delay(100);

        // Act
        var state = _gameService.GetGameState(session!.GameId);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(session.GameId, state.GameId);
        Assert.Equal(room.RoomId, state.RoomId);
    }

    [Fact]
    public void GetGameState_WithInvalidId_ReturnsNull()
    {
        // Act
        var state = _gameService.GetGameState("nonexistent");

        // Assert
        Assert.Null(state);
    }

    #endregion

    #region GetPlayerState Tests

    [Fact]
    public async Task GetPlayerState_ReturnsPlayerSpecificState()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room);

        // Wait briefly for game to initialize
        await Task.Delay(100);

        // Act
        var state = _gameService.GetPlayerState(session!.GameId, clientId);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(PlayerPosition.Bottom, state.Position);
        Assert.NotNull(state.GameState);
    }

    [Fact]
    public void GetPlayerState_WithInvalidClientId_ReturnsNull()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room);

        // Act
        var state = _gameService.GetPlayerState(session!.GameId, "invalid_client");

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public void GetPlayerState_WithInvalidGameId_ReturnsNull()
    {
        // Act
        var state = _gameService.GetPlayerState("nonexistent", "any_client");

        // Assert
        Assert.Null(state);
    }

    #endregion

    #region GetWatcherState Tests

    [Fact]
    public async Task GetWatcherState_ReturnsStateWithoutHands()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room);

        // Wait briefly for game to initialize
        await Task.Delay(100);

        // Act
        var state = _gameService.GetWatcherState(session!.GameId);

        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.GameState);
        Assert.NotNull(state.PlayerCardCounts);
    }

    [Fact]
    public void GetWatcherState_WithInvalidId_ReturnsNull()
    {
        // Act
        var state = _gameService.GetWatcherState("nonexistent");

        // Assert
        Assert.Null(state);
    }

    #endregion

    #region SubmitCut Tests

    [Fact]
    public async Task SubmitCut_WithValidAction_CompletesAction()
    {
        // Arrange
        var session = await CreateSessionWithCutPending();
        var clientId = session.ClientPositions.First(kv => kv.Value == session.PendingAction!.Player).Key;

        // Act
        var result = _gameService.SubmitCut(session.GameId, clientId, 16, true);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.Value, 15, 17);
    }

    [Fact]
    public void SubmitCut_AppliesNudgeWithinOneCard()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        for (var i = 0; i < 50; i++)
        {
            var pending = new PendingAction
            {
                ActionType = PendingActionType.Cut,
                Player = PlayerPosition.Bottom,
                CutTcs = new TaskCompletionSource<(int, bool)>(),
                TimeoutDuration = TimeSpan.FromMinutes(2)
            };
            session.PendingActions[PlayerPosition.Bottom] = pending;

            // Act
            var result = _gameService.SubmitCut(session.GameId, clientId, 16, false);

            // Assert - the nudged position stays within one card and the engine
            // receives exactly the value returned to the client
            Assert.NotNull(result);
            Assert.InRange(result.Value, 15, 17);
            var (enginePosition, fromTop) = pending.CutTcs.Task.Result;
            Assert.Equal(result.Value, enginePosition);
            Assert.False(fromTop);
        }
    }

    [Fact]
    public void SubmitCut_ClampsNudgeToValidRange()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        for (var i = 0; i < 50; i++)
        {
            session.PendingActions[PlayerPosition.Bottom] = new PendingAction
            {
                ActionType = PendingActionType.Cut,
                Player = PlayerPosition.Bottom,
                CutTcs = new TaskCompletionSource<(int, bool)>(),
                TimeoutDuration = TimeSpan.FromMinutes(2)
            };
            var atMin = _gameService.SubmitCut(session.GameId, clientId, 6, true);
            Assert.NotNull(atMin);
            Assert.InRange(atMin.Value, 6, 7);

            session.PendingActions[PlayerPosition.Bottom] = new PendingAction
            {
                ActionType = PendingActionType.Cut,
                Player = PlayerPosition.Bottom,
                CutTcs = new TaskCompletionSource<(int, bool)>(),
                TimeoutDuration = TimeSpan.FromMinutes(2)
            };
            var atMax = _gameService.SubmitCut(session.GameId, clientId, 26, true);
            Assert.NotNull(atMax);
            Assert.InRange(atMax.Value, 25, 26);
        }
    }

    [Fact]
    public void SubmitCut_NudgeVaries()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;
        var results = new HashSet<int>();

        for (var i = 0; i < 100; i++)
        {
            session.PendingActions[PlayerPosition.Bottom] = new PendingAction
            {
                ActionType = PendingActionType.Cut,
                Player = PlayerPosition.Bottom,
                CutTcs = new TaskCompletionSource<(int, bool)>(),
                TimeoutDuration = TimeSpan.FromMinutes(2)
            };
            results.Add(_gameService.SubmitCut(session.GameId, clientId, 16, true)!.Value);
        }

        // P(all 100 nudges identical) = (1/3)^99 - effectively impossible
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public void SubmitCut_WithInvalidPosition_ReturnsNull()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        // Manually create a cut pending action for testing
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = PlayerPosition.Bottom,
            CutTcs = new TaskCompletionSource<(int, bool)>(),
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act - Invalid position (out of range)
        var result = _gameService.SubmitCut(session.GameId, clientId, 5, true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SubmitCut_WhenNoPendingAction_ReturnsNull()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room);

        // Act
        var result = _gameService.SubmitCut(session!.GameId, clientId, 16, true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SubmitCut_WhenWrongActionType_ReturnsNull()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = PlayerPosition.Bottom,
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCut(session.GameId, clientId, 16, true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SubmitCut_WhenWrongPlayer_ReturnsNull()
    {
        // Arrange
        var room = CreateTestRoomWithMultipleHumans();
        var wrongClientId = room.PlayerSlots[PlayerPosition.Top]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = PlayerPosition.Bottom,
            CutTcs = new TaskCompletionSource<(int, bool)>(),
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCut(session.GameId, wrongClientId, 16, true);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region SubmitNegotiation Tests

    [Fact]
    public void SubmitNegotiation_WithValidAction_CompletesAction()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        var validActions = new List<NegotiationAction> { new AcceptAction(PlayerPosition.Bottom, new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts)) };
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.Negotiate,
            Player = PlayerPosition.Bottom,
            NegotiationTcs = new TaskCompletionSource<NegotiationAction>(),
            ValidNegotiationActions = validActions,
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitNegotiation(session.GameId, clientId, new AcceptAction(PlayerPosition.Bottom, new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts)));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SubmitNegotiation_WithInvalidAction_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        var validActions = new List<NegotiationAction> { new AcceptAction(PlayerPosition.Bottom, new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts)) };
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.Negotiate,
            Player = PlayerPosition.Bottom,
            NegotiationTcs = new TaskCompletionSource<NegotiationAction>(),
            ValidNegotiationActions = validActions,
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act - Try to announce when only accept is valid
        var result = _gameService.SubmitNegotiation(session.GameId, clientId, new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitNegotiation_WhenNoPendingAction_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room);

        // Act
        var result = _gameService.SubmitNegotiation(session!.GameId, clientId, new AcceptAction(PlayerPosition.Bottom, new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts)));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region SubmitCardPlay Tests

    [Fact]
    public void SubmitCardPlay_WithValidCard_CompletesAction()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        var validCard = new Card(CardRank.Ace, CardSuit.Spades);
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = PlayerPosition.Bottom,
            PlayCardTcs = new TaskCompletionSource<Card>(),
            ValidCards = [validCard],
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCardPlay(session.GameId, clientId, validCard);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SubmitCardPlay_WithInvalidCard_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        var validCard = new Card(CardRank.Ace, CardSuit.Spades);
        var invalidCard = new Card(CardRank.Seven, CardSuit.Hearts);
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = PlayerPosition.Bottom,
            PlayCardTcs = new TaskCompletionSource<Card>(),
            ValidCards = [validCard],
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCardPlay(session.GameId, clientId, invalidCard);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitCardPlay_WhenNoPendingAction_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room);

        // Act
        var result = _gameService.SubmitCardPlay(session!.GameId, clientId, new Card(CardRank.Ace, CardSuit.Spades));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitCardPlay_WithInvalidGameId_ReturnsFalse()
    {
        // Act
        var result = _gameService.SubmitCardPlay("nonexistent", "any", new Card(CardRank.Ace, CardSuit.Spades));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region AbandonGame Tests

    private IMatchPersistenceService WireAbandonDependencies()
    {
        var persistence = Substitute.For<IMatchPersistenceService>();
        var scopedProvider = Substitute.For<IServiceProvider>();
        scopedProvider.GetService(typeof(IMatchPersistenceService)).Returns(persistence);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopedProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        _serviceProvider.GetService(typeof(IRoomService)).Returns(Substitute.For<IRoomService>());
        return persistence;
    }

    [Fact]
    public void SubmitCut_MarksPlayerActed()
    {
        // Arrange — hand-built session (no game loop) so the pending action is stable
        var session = new GameSession
        {
            GameId = "game_acted",
            RoomId = "room_test",
            PlayerAgents = new Dictionary<PlayerPosition, IPlayerAgent>(),
            ClientPositions = new Dictionary<string, PlayerPosition> { ["client_human1"] = PlayerPosition.Bottom },
            PlayerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>()
        };
        _gameRepository.Add(session);
        session.PendingActions[PlayerPosition.Bottom] = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = PlayerPosition.Bottom,
            CutTcs = new TaskCompletionSource<(int, bool)>(),
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        Assert.False(session.PlayersActed.ContainsKey(PlayerPosition.Bottom));
        _gameService.SubmitCut(session.GameId, "client_human1", 16, true);

        // Assert
        Assert.True(session.PlayersActed.ContainsKey(PlayerPosition.Bottom));
    }

    [Fact]
    public async Task AbandonGame_BeforeAnyAction_DoesNotPersistForfeit()
    {
        // Arrange — reflex-click scenario: the player quits without ever acting
        var persistence = WireAbandonDependencies();
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room)!;

        // Act
        await _gameService.AbandonGameAsync(session.GameId, PlayerPosition.Bottom);

        // Assert
        await persistence.DidNotReceive()
            .PersistAbandonedMatchAsync(Arg.Any<GameSession>(), Arg.Any<PlayerPosition>());
    }

    [Fact]
    public async Task AbandonGame_AfterPlayerActed_PersistsForfeit()
    {
        // Arrange
        var persistence = WireAbandonDependencies();
        var room = CreateTestRoomWithHumanPlayer();
        var session = _gameService.CreateGame(room)!;
        session.PlayersActed[PlayerPosition.Bottom] = true;

        // Act
        await _gameService.AbandonGameAsync(session.GameId, PlayerPosition.Bottom);

        // Assert
        await persistence.Received(1)
            .PersistAbandonedMatchAsync(session, PlayerPosition.Bottom);
    }

    #endregion

    #region Helper Methods

    private static Room CreateTestRoomWithHumanPlayer()
    {
        var client = new ConnectedClient
        {
            ClientId = "client_human1",
            DisplayName = "Human Player",
            IsPlayer = true,
            Position = PlayerPosition.Bottom
        };

        var room = new Room
        {
            RoomId = "room_test",
            Name = "Test Room",
            CreatorClientId = client.ClientId,
            OwnerUserId = Guid.NewGuid()
        };

        room.PlayerSlots[PlayerPosition.Bottom] = client;

        return room;
    }

    private static Room CreateTestRoomWithMultipleHumans()
    {
        var client1 = new ConnectedClient
        {
            ClientId = "client_human1",
            DisplayName = "Human Player 1",
            IsPlayer = true,
            Position = PlayerPosition.Bottom
        };

        var client2 = new ConnectedClient
        {
            ClientId = "client_human2",
            DisplayName = "Human Player 2",
            IsPlayer = true,
            Position = PlayerPosition.Top
        };

        var room = new Room
        {
            RoomId = "room_test",
            Name = "Test Room",
            CreatorClientId = client1.ClientId,
            OwnerUserId = Guid.NewGuid()
        };

        room.PlayerSlots[PlayerPosition.Bottom] = client1;
        room.PlayerSlots[PlayerPosition.Top] = client2;

        return room;
    }

    private async Task<GameSession> CreateSessionWithCutPending()
    {
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        // Wait for the game to request a cut
        for (var i = 0; i < 50; i++)
        {
            if (session.PendingAction?.ActionType == PendingActionType.Cut)
                break;
            await Task.Delay(50);
        }

        // If no pending action, create one manually for testing
        if (!session.PendingActions.TryGetValue(PlayerPosition.Bottom, out var pa) || pa.ActionType != PendingActionType.Cut)
        {
            session.PendingActions[PlayerPosition.Bottom] = new PendingAction
            {
                ActionType = PendingActionType.Cut,
                Player = PlayerPosition.Bottom,
                CutTcs = new TaskCompletionSource<(int, bool)>(),
                TimeoutDuration = TimeSpan.FromMinutes(2)
            };
        }

        return session;
    }

    #endregion
}
