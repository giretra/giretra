using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Integration tests for <see cref="IRoomService"/>.
/// </summary>
public sealed class RoomServiceTests
{
    private readonly IRoomRepository _roomRepository;
    private readonly IGameService _gameService;
    private readonly INotificationService _notifications;
    private readonly RoomService _roomService;

    public RoomServiceTests()
    {
        _roomRepository = new InMemoryRoomRepository();
        _gameService = Substitute.For<IGameService>();
        _notifications = Substitute.For<INotificationService>();
        _roomService = new RoomService(_roomRepository, _gameService, _notifications);
    }

    #region CreateRoom Tests

    [Fact]
    public void CreateRoom_WithValidRequest_CreatesRoomAndReturnsClientId()
    {
        // Arrange
        var request = new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player1",
            AiSeats = null
        };

        // Act
        var response = _roomService.CreateRoom(request, "Player1");

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.ClientId);
        Assert.StartsWith("client_", response.ClientId);
        Assert.Equal(PlayerPosition.Bottom, response.Position);
        Assert.NotNull(response.Room);
        Assert.Equal("Test Room", response.Room.Name);
        Assert.Equal(1, response.Room.PlayerCount);
    }

    [Fact]
    public void CreateRoom_WithFillWithAi_MarksAiSlots()
    {
        // Arrange
        var request = new CreateRoomRequest
        {
            Name = "AI Room",
            CreatorName = "Human",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ]
        };

        // Act
        var response = _roomService.CreateRoom(request, "Human");

        // Assert
        Assert.NotNull(response.Room);
        Assert.Equal(1, response.Room.PlayerCount);

        // Check AI slots are marked
        var bottomSlot = response.Room.PlayerSlots.First(s => s.Position == PlayerPosition.Bottom);
        var leftSlot = response.Room.PlayerSlots.First(s => s.Position == PlayerPosition.Left);
        var topSlot = response.Room.PlayerSlots.First(s => s.Position == PlayerPosition.Top);
        var rightSlot = response.Room.PlayerSlots.First(s => s.Position == PlayerPosition.Right);

        Assert.False(bottomSlot.IsAi);
        Assert.True(leftSlot.IsAi);
        Assert.True(topSlot.IsAi);
        Assert.True(rightSlot.IsAi);
    }

    [Fact]
    public void CreateRoom_StoresRoomInRepository()
    {
        // Arrange
        var request = new CreateRoomRequest
        {
            Name = "Stored Room",
            CreatorName = "Creator",
            AiSeats = null
        };

        // Act
        var response = _roomService.CreateRoom(request, "Creator");

        // Assert
        var storedRoom = _roomRepository.GetById(response.Room.RoomId);
        Assert.NotNull(storedRoom);
        Assert.Equal("Stored Room", storedRoom.Name);
    }

    #endregion

    #region GetRoom Tests

    [Fact]
    public void GetRoom_WithValidId_ReturnsRoom()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player1",
            AiSeats = null
        }, "Player1");

        // Act
        var room = _roomService.GetRoom(createResponse.Room.RoomId);

        // Assert
        Assert.NotNull(room);
        Assert.Equal("Test Room", room.Name);
    }

    [Fact]
    public void GetRoom_WithInvalidId_ReturnsNull()
    {
        // Act
        var room = _roomService.GetRoom("nonexistent_room");

        // Assert
        Assert.Null(room);
    }

    #endregion

    #region GetAllRooms Tests

    [Fact]
    public void GetAllRooms_ReturnsAllRooms()
    {
        // Arrange
        _roomService.CreateRoom(new CreateRoomRequest { Name = "Room 1", CreatorName = "P1", AiSeats = null }, "P1");
        _roomService.CreateRoom(new CreateRoomRequest { Name = "Room 2", CreatorName = "P2", AiSeats = null }, "P2");

        // Act
        var response = _roomService.GetAllRooms();

        // Assert
        Assert.Equal(2, response.TotalCount);
        Assert.Contains(response.Rooms, r => r.Name == "Room 1");
        Assert.Contains(response.Rooms, r => r.Name == "Room 2");
    }

    #endregion

    #region JoinRoom Tests

    [Fact]
    public void JoinRoom_WithValidRequest_AddsPlayerToRoom()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        var joinRequest = new JoinRoomRequest
        {
            DisplayName = "Player2"
        };

        // Act
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, joinRequest, "Player2");

        // Assert
        Assert.NotNull(joinResponse);
        Assert.NotNull(joinResponse.ClientId);
        Assert.NotEqual(createResponse.ClientId, joinResponse.ClientId);
        Assert.NotNull(joinResponse.Position);
        Assert.Equal(2, joinResponse.Room.PlayerCount);
    }

    [Fact]
    public void JoinRoom_WithPreferredPosition_AssignsToPreferredPosition()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        var joinRequest = new JoinRoomRequest
        {
            DisplayName = "Player2",
            PreferredPosition = PlayerPosition.Top
        };

        // Act
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, joinRequest, "Player2");

        // Assert
        Assert.NotNull(joinResponse);
        Assert.Equal(PlayerPosition.Top, joinResponse.Position);
    }

    [Fact]
    public void JoinRoom_WhenPositionOccupied_ReturnsNull()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        var joinRequest = new JoinRoomRequest
        {
            DisplayName = "Player2",
            PreferredPosition = PlayerPosition.Bottom // Already occupied by creator
        };

        // Act
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, joinRequest, "Player2");

        // Assert
        Assert.Null(joinResponse);
    }

    [Fact]
    public void JoinRoom_WhenRoomFull_ReturnsNull()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ] // Fills with AI
        }, "Creator");

        var joinRequest = new JoinRoomRequest
        {
            DisplayName = "Player5"
        };

        // Act
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, joinRequest, "Player5");

        // Assert
        Assert.Null(joinResponse);
    }

    [Fact]
    public void JoinRoom_WhenRoomNotFound_ReturnsNull()
    {
        // Act
        var joinResponse = _roomService.JoinRoom("nonexistent", new JoinRoomRequest { DisplayName = "Player" }, "Player");

        // Assert
        Assert.Null(joinResponse);
    }

    [Fact]
    public void JoinRoom_AssignsFirstAvailablePosition()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act - Join 3 more players without specifying positions
        var join1 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P2" }, "P2");
        var join2 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P3" }, "P3");
        var join3 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P4" }, "P4");

        // Assert - All should get different positions
        var positions = new[] { createResponse.Position, join1!.Position, join2!.Position, join3!.Position };
        Assert.Equal(4, positions.Distinct().Count());
    }

    #endregion

    #region WatchRoom Tests

    [Fact]
    public void WatchRoom_WithValidRequest_AddsWatcher()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        var watchRequest = new JoinRoomRequest
        {
            DisplayName = "Spectator"
        };

        // Act
        var watchResponse = _roomService.WatchRoom(createResponse.Room.RoomId, watchRequest, "Spectator");

        // Assert
        Assert.NotNull(watchResponse);
        Assert.NotNull(watchResponse.ClientId);
        Assert.Null(watchResponse.Position); // Watchers don't have a position
        Assert.Equal(1, watchResponse.Room.WatcherCount);
    }

    [Fact]
    public void WatchRoom_WhenRoomNotFound_ReturnsNull()
    {
        // Act
        var response = _roomService.WatchRoom("nonexistent", new JoinRoomRequest { DisplayName = "Watcher" }, "Watcher");

        // Assert
        Assert.Null(response);
    }

    #endregion

    #region LeaveRoom Tests

    [Fact]
    public void LeaveRoom_AsPlayer_RemovesPlayer()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P2" }, "P2");

        // Act
        var result = _roomService.LeaveRoom(createResponse.Room.RoomId, joinResponse!.ClientId);

        // Assert
        Assert.True(result.Removed);
        Assert.Equal("P2", result.PlayerName);
        Assert.NotNull(result.Position);
        var room = _roomService.GetRoom(createResponse.Room.RoomId);
        Assert.Equal(1, room!.PlayerCount);
    }

    [Fact]
    public void LeaveRoom_AsWatcher_RemovesWatcher()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        var watchResponse = _roomService.WatchRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Watcher" }, "Watcher");

        // Act
        var result = _roomService.LeaveRoom(createResponse.Room.RoomId, watchResponse!.ClientId);

        // Assert
        Assert.True(result.Removed);
        Assert.Null(result.PlayerName);
        Assert.Null(result.Position);
        var room = _roomService.GetRoom(createResponse.Room.RoomId);
        Assert.Equal(0, room!.WatcherCount);
    }

    [Fact]
    public void LeaveRoom_WithInvalidClientId_ReturnsFalse()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act
        var result = _roomService.LeaveRoom(createResponse.Room.RoomId, "invalid_client");

        // Assert
        Assert.False(result.Removed);
    }

    #endregion

    #region DeleteRoom Tests

    [Fact]
    public void DeleteRoom_AsCreator_DeletesRoom()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act
        var result = _roomService.DeleteRoom(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        Assert.True(result);
        Assert.Null(_roomService.GetRoom(createResponse.Room.RoomId));
    }

    [Fact]
    public void DeleteRoom_NotAsCreator_ReturnsFalse()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P2" }, "P2");

        // Act
        var result = _roomService.DeleteRoom(createResponse.Room.RoomId, joinResponse!.ClientId);

        // Assert
        Assert.False(result);
        Assert.NotNull(_roomService.GetRoom(createResponse.Room.RoomId));
    }

    [Fact]
    public void DeleteRoom_WhenRoomNotFound_ReturnsFalse()
    {
        // Act
        var result = _roomService.DeleteRoom("nonexistent", "any_client");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region StartGame Tests

    [Fact]
    public void StartGame_WithValidSetup_StartsGame()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ]
        }, "Creator");

        var mockSession = new GameSession
        {
            GameId = "game_123",
            RoomId = createResponse.Room.RoomId,
            PlayerAgents = new Dictionary<PlayerPosition, IPlayerAgent>(),
            ClientPositions = new Dictionary<string, PlayerPosition>()
        };
        _gameService.CreateGame(Arg.Any<Room>()).Returns(mockSession);

        // Act
        var (response, error) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        Assert.NotNull(response);
        Assert.Null(error);
        Assert.Equal("game_123", response.GameId);
        _gameService.Received(1).CreateGame(Arg.Any<Room>());
    }

    [Fact]
    public void StartGame_NotAsCreator_ReturnsError()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "P2" }, "P2");

        // Act
        var (response, error) = _roomService.StartGame(createResponse.Room.RoomId, joinResponse!.ClientId);

        // Assert
        Assert.Null(response);
        Assert.Contains("Only the room creator can start the game", error);
    }

    [Fact]
    public void StartGame_WithNoPlayers_ReturnsError()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        // Remove the creator
        _roomService.LeaveRoom(createResponse.Room.RoomId, createResponse.ClientId);

        // Act
        var (response, error) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        Assert.Null(response);
        Assert.NotNull(error);
    }

    [Fact]
    public void StartGame_WhenRoomNotFound_ReturnsError()
    {
        // Act
        var (response, error) = _roomService.StartGame("nonexistent", "any_client");

        // Assert
        Assert.Null(response);
        Assert.Equal("Room not found", error);
    }

    [Fact]
    public void StartGame_UpdatesRoomStatus()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ]
        }, "Creator");

        var mockSession = new GameSession
        {
            GameId = "game_123",
            RoomId = createResponse.Room.RoomId,
            PlayerAgents = new Dictionary<PlayerPosition, IPlayerAgent>(),
            ClientPositions = new Dictionary<string, PlayerPosition>()
        };
        _gameService.CreateGame(Arg.Any<Room>()).Returns(mockSession);

        // Act
        _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        var room = _roomRepository.GetById(createResponse.Room.RoomId);
        Assert.Equal(RoomStatus.Playing, room!.Status);
        Assert.Equal("game_123", room.GameSessionId);
    }

    [Fact]
    public void StartGame_WhenAlreadyPlaying_ReturnsError()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ]
        }, "Creator");

        var mockSession = new GameSession
        {
            GameId = "game_123",
            RoomId = createResponse.Room.RoomId,
            PlayerAgents = new Dictionary<PlayerPosition, IPlayerAgent>(),
            ClientPositions = new Dictionary<string, PlayerPosition>()
        };
        _gameService.CreateGame(Arg.Any<Room>()).Returns(mockSession);

        // Start game first time
        _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Act - Try to start again
        var (response, error) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        Assert.Null(response);
        Assert.Contains("not in waiting state", error);
    }

    [Fact]
    public void StartGame_NotifiesClients()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats =
            [
                new() { Position = PlayerPosition.Left, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Top, AiType = "CalculatingPlayer" },
                new() { Position = PlayerPosition.Right, AiType = "CalculatingPlayer" }
            ]
        }, "Creator");

        var mockSession = new GameSession
        {
            GameId = "game_123",
            RoomId = createResponse.Room.RoomId,
            PlayerAgents = new Dictionary<PlayerPosition, IPlayerAgent>(),
            ClientPositions = new Dictionary<string, PlayerPosition>()
        };
        _gameService.CreateGame(Arg.Any<Room>()).Returns(mockSession);

        // Act
        _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        _notifications.Received(1).NotifyGameStartedAsync(createResponse.Room.RoomId, "game_123");
    }

    #endregion

    #region GetRoomForClient Tests

    [Fact]
    public void GetRoomForClient_WithValidPlayer_ReturnsRoom()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act
        var room = _roomService.GetRoomForClient(createResponse.ClientId);

        // Assert
        Assert.NotNull(room);
        Assert.Equal(createResponse.Room.RoomId, room.RoomId);
    }

    [Fact]
    public void GetRoomForClient_WithWatcher_ReturnsRoom()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");
        var watchResponse = _roomService.WatchRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Watcher" }, "Watcher");

        // Act
        var room = _roomService.GetRoomForClient(watchResponse!.ClientId);

        // Assert
        Assert.NotNull(room);
        Assert.Equal(createResponse.Room.RoomId, room.RoomId);
    }

    [Fact]
    public void GetRoomForClient_WithInvalidClientId_ReturnsNull()
    {
        // Arrange
        _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act
        var room = _roomService.GetRoomForClient("invalid_client");

        // Assert
        Assert.Null(room);
    }

    #endregion

    #region UpdateClientConnection Tests

    [Fact]
    public void UpdateClientConnection_UpdatesConnectionId()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            AiSeats = null
        }, "Creator");

        // Act
        _roomService.UpdateClientConnection(createResponse.ClientId, "connection_abc");

        // Assert
        var room = _roomRepository.GetById(createResponse.Room.RoomId);
        var client = room!.GetClient(createResponse.ClientId);
        Assert.Equal("connection_abc", client!.ConnectionId);
    }

    #endregion
}
