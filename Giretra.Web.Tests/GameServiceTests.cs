using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
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
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _gameRepository = new InMemoryGameRepository();
        _roomRepository = new InMemoryRoomRepository();
        _notifications = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<GameService>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        var aiRegistry = new AiPlayerRegistry();
        _gameService = new GameService(_gameRepository, _roomRepository, _notifications, aiRegistry, _logger, _loggerFactory);
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
        Assert.True(result);
    }

    [Fact]
    public void SubmitCut_WithInvalidPosition_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        // Manually create a cut pending action for testing
        session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = PlayerPosition.Bottom,
            CutTcs = new TaskCompletionSource<(int, bool)>(),
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act - Invalid position (out of range)
        var result = _gameService.SubmitCut(session.GameId, clientId, 5, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitCut_WhenNoPendingAction_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room);

        // Act
        var result = _gameService.SubmitCut(session!.GameId, clientId, 16, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitCut_WhenWrongActionType_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithHumanPlayer();
        var clientId = room.PlayerSlots[PlayerPosition.Bottom]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = PlayerPosition.Bottom,
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCut(session.GameId, clientId, 16, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SubmitCut_WhenWrongPlayer_ReturnsFalse()
    {
        // Arrange
        var room = CreateTestRoomWithMultipleHumans();
        var wrongClientId = room.PlayerSlots[PlayerPosition.Top]!.ClientId;
        var session = _gameService.CreateGame(room)!;

        session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = PlayerPosition.Bottom,
            CutTcs = new TaskCompletionSource<(int, bool)>(),
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitCut(session.GameId, wrongClientId, 16, true);

        // Assert
        Assert.False(result);
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

        var validActions = new List<NegotiationAction> { new AcceptAction(PlayerPosition.Bottom) };
        session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.Negotiate,
            Player = PlayerPosition.Bottom,
            NegotiationTcs = new TaskCompletionSource<NegotiationAction>(),
            ValidNegotiationActions = validActions,
            TimeoutDuration = TimeSpan.FromMinutes(2)
        };

        // Act
        var result = _gameService.SubmitNegotiation(session.GameId, clientId, new AcceptAction(PlayerPosition.Bottom));

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

        var validActions = new List<NegotiationAction> { new AcceptAction(PlayerPosition.Bottom) };
        session.PendingAction = new PendingAction
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
        var result = _gameService.SubmitNegotiation(session!.GameId, clientId, new AcceptAction(PlayerPosition.Bottom));

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
        session.PendingAction = new PendingAction
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
        session.PendingAction = new PendingAction
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
            CreatorClientId = client.ClientId
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
            CreatorClientId = client1.ClientId
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
        if (session.PendingAction == null || session.PendingAction.ActionType != PendingActionType.Cut)
        {
            session.PendingAction = new PendingAction
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
