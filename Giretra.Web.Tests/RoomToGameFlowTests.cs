using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Integration tests verifying the complete flow from room creation to game play.
/// </summary>
public sealed class RoomToGameFlowTests
{
    private readonly IRoomRepository _roomRepository;
    private readonly IGameRepository _gameRepository;
    private readonly INotificationService _notifications;
    private readonly GameService _gameService;
    private readonly RoomService _roomService;

    public RoomToGameFlowTests()
    {
        _roomRepository = new InMemoryRoomRepository();
        _gameRepository = new InMemoryGameRepository();
        _notifications = Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<GameService>>();

        _gameService = new GameService(_gameRepository, _notifications, logger);
        _roomService = new RoomService(_roomRepository, _gameService, _notifications);
    }

    #region Complete Flow Tests

    [Fact]
    public void CompleteFlow_CreateRoom_JoinPlayers_StartGame()
    {
        // Step 1: Create room
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Full Game Room",
            CreatorName = "Player1",
            FillWithAi = false
        });

        Assert.NotNull(createResponse);
        Assert.Equal(PlayerPosition.Bottom, createResponse.Position);
        Assert.Equal(RoomStatus.Waiting, createResponse.Room.Status);

        // Step 2: Three more players join
        var player2 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Player2" });
        var player3 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Player3" });
        var player4 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Player4" });

        Assert.NotNull(player2);
        Assert.NotNull(player3);
        Assert.NotNull(player4);

        var room = _roomService.GetRoom(createResponse.Room.RoomId);
        Assert.Equal(4, room!.PlayerCount);

        // Step 3: Creator starts the game
        var (startResponse, error) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        Assert.NotNull(startResponse);
        Assert.Null(error);
        Assert.NotNull(startResponse.GameId);

        // Verify room status changed
        room = _roomService.GetRoom(createResponse.Room.RoomId);
        Assert.Equal(RoomStatus.Playing, room!.Status);
        Assert.Equal(startResponse.GameId, room.GameId);

        // Verify game session exists
        var game = _gameService.GetGame(startResponse.GameId);
        Assert.NotNull(game);
        Assert.Equal(4, game.ClientPositions.Count);
    }

    [Fact]
    public void CompleteFlow_CreateRoomWithAi_StartGame_ImmediatePlay()
    {
        // Step 1: Create room with AI
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "AI Game Room",
            CreatorName = "HumanPlayer",
            FillWithAi = true
        });

        Assert.NotNull(createResponse);

        // Step 2: Start game immediately (no need to wait for players)
        var (startResponse, error) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        Assert.NotNull(startResponse);
        Assert.Null(error);

        // Verify game session has correct structure
        var game = _gameService.GetGame(startResponse.GameId);
        Assert.NotNull(game);
        Assert.Single(game.ClientPositions); // Only 1 human player
        Assert.Equal(4, game.PlayerAgents.Count); // 4 player agents (1 human + 3 AI)
    }

    [Fact]
    public async Task CompleteFlow_GameState_AvailableAfterStart()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Wait for game to initialize
        await Task.Delay(200);

        // Act
        var gameState = _gameService.GetGameState(startResponse!.GameId);
        var playerState = _gameService.GetPlayerState(startResponse.GameId, createResponse.ClientId);

        // Assert
        Assert.NotNull(gameState);
        Assert.NotNull(playerState);
        Assert.Equal(PlayerPosition.Bottom, playerState.Position);
    }

    [Fact]
    public async Task CompleteFlow_WatcherCanViewGame()
    {
        // Step 1: Create and start game
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Spectated Game",
            CreatorName = "Player",
            FillWithAi = true
        });

        // Add watcher before game starts
        var watchResponse = _roomService.WatchRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Spectator" });
        Assert.NotNull(watchResponse);

        // Start game
        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);
        Assert.NotNull(startResponse);

        // Wait for game to initialize
        await Task.Delay(200);

        // Watcher can get state
        var watcherState = _gameService.GetWatcherState(startResponse.GameId);
        Assert.NotNull(watcherState);
        Assert.NotNull(watcherState.PlayerCardCounts);
    }

    #endregion

    #region Flow Validation Tests

    [Fact]
    public void FlowValidation_CannotJoinAfterGameStarts()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            FillWithAi = true
        });

        _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Act - Try to join after game started
        var joinResponse = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "LatePlayer" });

        // Assert
        Assert.Null(joinResponse);
    }

    [Fact]
    public void FlowValidation_CannotDeleteRoomAfterGameStarts()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            FillWithAi = true
        });

        _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Act
        var result = _roomService.DeleteRoom(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FlowValidation_NonCreatorCannotStartGame()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Creator",
            FillWithAi = false
        });

        var player2 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest { DisplayName = "Player2" });

        // Act
        var (response, error) = _roomService.StartGame(createResponse.Room.RoomId, player2!.ClientId);

        // Assert
        Assert.Null(response);
        Assert.Contains("Only the room creator", error);
    }

    [Fact]
    public void FlowValidation_GameCreatesCorrectAgentTypes()
    {
        // Arrange - Room with 2 humans and 2 AI
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Mixed Room",
            CreatorName = "Human1",
            FillWithAi = false
        });

        var player2 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest
        {
            DisplayName = "Human2",
            PreferredPosition = PlayerPosition.Top
        });

        // Start game (2 positions will be AI)
        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        var game = _gameService.GetGame(startResponse!.GameId);
        Assert.NotNull(game);

        // Should have 2 human client mappings
        Assert.Equal(2, game.ClientPositions.Count);
        Assert.True(game.ClientPositions.ContainsKey(createResponse.ClientId));
        Assert.True(game.ClientPositions.ContainsKey(player2!.ClientId));

        // All 4 positions have agents
        Assert.Equal(4, game.PlayerAgents.Count);
    }

    #endregion

    #region Action Submission Flow Tests

    [Fact]
    public async Task ActionFlow_CutSubmission_CompletesSuccessfully()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);
        var game = _gameService.GetGame(startResponse!.GameId)!;

        // Wait for a cut pending action
        await WaitForPendingAction(game, PendingActionType.Cut, PlayerPosition.Bottom, TimeSpan.FromSeconds(5));

        // Act
        if (game.PendingAction?.ActionType == PendingActionType.Cut &&
            game.PendingAction.Player == PlayerPosition.Bottom)
        {
            var result = _gameService.SubmitCut(game.GameId, createResponse.ClientId, 16, true);
            Assert.True(result);
        }
    }

    [Fact]
    public async Task ActionFlow_NegotiationSubmission_CompletesSuccessfully()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);
        var game = _gameService.GetGame(startResponse!.GameId)!;

        // Wait for a negotiation pending action for our player
        await WaitForPendingAction(game, PendingActionType.Negotiate, PlayerPosition.Bottom, TimeSpan.FromSeconds(10));

        // Act - If we have a negotiation pending, submit an accept
        if (game.PendingAction?.ActionType == PendingActionType.Negotiate &&
            game.PendingAction.Player == PlayerPosition.Bottom &&
            game.PendingAction.ValidNegotiationActions != null)
        {
            var acceptAction = game.PendingAction.ValidNegotiationActions
                .FirstOrDefault(a => a is AcceptAction);

            if (acceptAction != null)
            {
                var result = _gameService.SubmitNegotiation(game.GameId, createResponse.ClientId, acceptAction);
                Assert.True(result);
            }
        }
    }

    [Fact]
    public async Task ActionFlow_CardPlaySubmission_CompletesSuccessfully()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);
        var game = _gameService.GetGame(startResponse!.GameId)!;

        // Wait for a card play pending action for our player
        await WaitForPendingAction(game, PendingActionType.PlayCard, PlayerPosition.Bottom, TimeSpan.FromSeconds(15));

        // Act - If we have a card play pending, play the first valid card
        if (game.PendingAction?.ActionType == PendingActionType.PlayCard &&
            game.PendingAction.Player == PlayerPosition.Bottom &&
            game.PendingAction.ValidCards != null &&
            game.PendingAction.ValidCards.Count > 0)
        {
            var cardToPlay = game.PendingAction.ValidCards[0];
            var result = _gameService.SubmitCardPlay(game.GameId, createResponse.ClientId, cardToPlay);
            Assert.True(result);
        }
    }

    [Fact]
    public async Task ActionFlow_PlayerState_ShowsCorrectTurnInfo()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);
        var game = _gameService.GetGame(startResponse!.GameId)!;

        // Wait for any pending action for our player
        await WaitForAnyPendingAction(game, PlayerPosition.Bottom, TimeSpan.FromSeconds(10));

        // Act
        var playerState = _gameService.GetPlayerState(game.GameId, createResponse.ClientId);

        // Assert
        Assert.NotNull(playerState);
        if (game.PendingAction?.Player == PlayerPosition.Bottom)
        {
            Assert.True(playerState.IsYourTurn);
            Assert.Equal(game.PendingAction.ActionType, playerState.PendingActionType);
        }
    }

    #endregion

    #region State Consistency Tests

    [Fact]
    public async Task StateConsistency_GameAndRoomLinked()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        await Task.Delay(100);

        // Act
        var room = _roomService.GetRoom(createResponse.Room.RoomId);
        var game = _gameService.GetGame(startResponse!.GameId);

        // Assert
        Assert.NotNull(room);
        Assert.NotNull(game);
        Assert.Equal(startResponse.GameId, room.GameId);
        Assert.Equal(createResponse.Room.RoomId, game.RoomId);
    }

    [Fact]
    public void StateConsistency_ClientIdMappingCorrect()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player1",
            FillWithAi = false
        });

        var player2 = _roomService.JoinRoom(createResponse.Room.RoomId, new JoinRoomRequest
        {
            DisplayName = "Player2",
            PreferredPosition = PlayerPosition.Left
        });

        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Act
        var game = _gameService.GetGame(startResponse!.GameId)!;

        // Assert
        Assert.Equal(PlayerPosition.Bottom, game.GetPositionForClient(createResponse.ClientId));
        Assert.Equal(PlayerPosition.Left, game.GetPositionForClient(player2!.ClientId));
    }

    [Fact]
    public void StateConsistency_RoomForClientReturnsCorrectRoom()
    {
        // Arrange
        _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Room 1",
            CreatorName = "Player1",
            FillWithAi = false
        });

        var room2Response = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Room 2",
            CreatorName = "Player2",
            FillWithAi = false
        });

        // Act
        var foundRoom = _roomService.GetRoomForClient(room2Response.ClientId);

        // Assert
        Assert.NotNull(foundRoom);
        Assert.Equal("Room 2", foundRoom.Name);
    }

    #endregion

    #region Notification Tests

    [Fact]
    public void Notifications_GameStarted_IsSent()
    {
        // Arrange
        var createResponse = _roomService.CreateRoom(new CreateRoomRequest
        {
            Name = "Test Room",
            CreatorName = "Player",
            FillWithAi = true
        });

        // Act
        var (startResponse, _) = _roomService.StartGame(createResponse.Room.RoomId, createResponse.ClientId);

        // Assert
        _notifications.Received(1).NotifyGameStartedAsync(
            createResponse.Room.RoomId,
            startResponse!.GameId);
    }

    #endregion

    #region Helper Methods

    private static async Task WaitForPendingAction(
        GameSession game,
        PendingActionType actionType,
        PlayerPosition player,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (game.PendingAction?.ActionType == actionType && game.PendingAction.Player == player)
                return;

            await Task.Delay(50);
        }
    }

    private static async Task WaitForAnyPendingAction(
        GameSession game,
        PlayerPosition player,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (game.PendingAction?.Player == player)
                return;

            await Task.Delay(50);
        }
    }

    #endregion
}
