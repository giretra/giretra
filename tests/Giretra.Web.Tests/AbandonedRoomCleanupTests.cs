using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// Tests for auto-deletion of Playing rooms where all human players have left
/// or disconnected (game continues bots-only until the abandoned timeout expires).
/// </summary>
public sealed class AbandonedRoomCleanupTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(150);
    private static readonly Guid CreatorUserId = Guid.NewGuid();

    private readonly IRoomRepository _roomRepository;
    private readonly IGameService _gameService;
    private readonly RoomService _roomService;

    public AbandonedRoomCleanupTests()
    {
        _roomRepository = new InMemoryRoomRepository();
        _gameService = Substitute.For<IGameService>();
        _gameService.CreateGame(Arg.Any<Room>()).Returns(ci => new GameSession
        {
            GameId = "game_test",
            RoomId = ci.Arg<Room>().RoomId,
            ClientPositions = [],
            PlayerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>()
        });

        _roomService = new RoomService(
            _roomRepository,
            _gameService,
            Substitute.For<INotificationService>(),
            Substitute.For<IChatService>(),
            AiPlayerRegistry.CreateFromAssembly(),
            Substitute.For<ILogger<RoomService>>(),
            abandonedRoomTimeout: ShortTimeout);
    }

    private (string RoomId, string ClientId) CreatePlayingRoom()
    {
        var (response, error) = _roomService.CreateRoom(
            new CreateRoomRequest { Name = "Test", CreatorName = "Player1" },
            "Player1",
            CreatorUserId);
        Assert.Null(error);

        var roomId = response!.Room.RoomId;
        var clientId = response.ClientId;

        _roomService.UpdateClientConnection(clientId, "conn_1");

        var (startResponse, startError) = _roomService.StartGame(roomId, CreatorUserId);
        Assert.Null(startError);
        Assert.NotNull(startResponse);

        return (roomId, clientId);
    }

    private async Task<bool> WaitForRoomRemovalAsync(string roomId)
    {
        for (var i = 0; i < 40; i++)
        {
            if (_roomRepository.GetById(roomId) == null)
                return true;
            await Task.Delay(50);
        }
        return false;
    }

    [Fact]
    public async Task Disconnect_LastHumanDuringGame_DeletesRoomAndTerminatesGameAfterTimeout()
    {
        var (roomId, _) = CreatePlayingRoom();

        _roomService.HandleDisconnect("conn_1");

        Assert.True(await WaitForRoomRemovalAsync(roomId));
        await _gameService.Received(1).TerminateGameAsync("game_test");
    }

    [Fact]
    public async Task Disconnect_HumanReconnectsBeforeTimeout_KeepsRoom()
    {
        var (roomId, clientId) = CreatePlayingRoom();

        _roomService.HandleDisconnect("conn_1");
        _roomService.UpdateClientConnection(clientId, "conn_2");

        await Task.Delay(ShortTimeout * 4);

        Assert.NotNull(_roomRepository.GetById(roomId));
        await _gameService.DidNotReceive().TerminateGameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task LeaveRoom_LastHumanDuringGame_KeepsRoomThenDeletesAfterTimeout()
    {
        var (roomId, clientId) = CreatePlayingRoom();

        var (removed, _, _) = _roomService.LeaveRoom(roomId, clientId);
        Assert.True(removed);

        // Not deleted immediately — the game session must not be orphaned
        Assert.NotNull(_roomRepository.GetById(roomId));

        Assert.True(await WaitForRoomRemovalAsync(roomId));
        await _gameService.Received(1).TerminateGameAsync("game_test");
    }

    [Fact]
    public async Task GameEndsBeforeTimeout_CancelsAbandonedCleanup()
    {
        var (roomId, _) = CreatePlayingRoom();

        _roomService.HandleDisconnect("conn_1");
        _roomService.ResetToWaiting(roomId);

        await Task.Delay(ShortTimeout * 4);

        var room = _roomRepository.GetById(roomId);
        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Waiting, room.Status);
        await _gameService.DidNotReceive().TerminateGameAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ConnectedWatcher_KeepsAbandonedRoomAlive()
    {
        var (roomId, _) = CreatePlayingRoom();

        var watchResponse = _roomService.WatchRoom(roomId, new JoinRoomRequest(), "Watcher");
        Assert.NotNull(watchResponse);
        _roomService.UpdateClientConnection(watchResponse.ClientId, "conn_watcher");

        _roomService.HandleDisconnect("conn_1");

        await Task.Delay(ShortTimeout * 4);

        Assert.NotNull(_roomRepository.GetById(roomId));
        await _gameService.DidNotReceive().TerminateGameAsync(Arg.Any<string>());
    }
}
