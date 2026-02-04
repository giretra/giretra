using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Repositories;

namespace Giretra.Web.Services;

/// <summary>
/// Service for managing game rooms.
/// </summary>
public sealed class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IGameService _gameService;
    private readonly INotificationService _notifications;
    private static int _roomCounter;

    public RoomService(IRoomRepository roomRepository, IGameService gameService, INotificationService notifications)
    {
        _roomRepository = roomRepository;
        _gameService = gameService;
        _notifications = notifications;
    }

    public RoomListResponse GetAllRooms()
    {
        var rooms = _roomRepository.GetAll().Select(MapToResponse).ToList();
        return new RoomListResponse
        {
            Rooms = rooms,
            TotalCount = rooms.Count
        };
    }

    public RoomResponse? GetRoom(string roomId)
    {
        var room = _roomRepository.GetById(roomId);
        return room != null ? MapToResponse(room) : null;
    }

    public JoinRoomResponse CreateRoom(CreateRoomRequest request)
    {
        var roomId = GenerateId("room");
        var clientId = GenerateId("client");

        var creator = new ConnectedClient
        {
            ClientId = clientId,
            DisplayName = request.CreatorName,
            IsPlayer = true,
            Position = PlayerPosition.Bottom
        };

        // Generate room name if not provided
        var roomName = string.IsNullOrWhiteSpace(request.Name)
            ? GenerateRoomName(request.CreatorName)
            : request.Name;

        var room = new Room
        {
            RoomId = roomId,
            Name = roomName,
            CreatorClientId = clientId
        };

        room.PlayerSlots[PlayerPosition.Bottom] = creator;

        // Fill other 3 seats with AI if requested
        if (request.FillWithAi)
        {
            room.AiSlots.Add(PlayerPosition.Left);
            room.AiSlots.Add(PlayerPosition.Top);
            room.AiSlots.Add(PlayerPosition.Right);
        }

        _roomRepository.Add(room);

        return new JoinRoomResponse
        {
            ClientId = clientId,
            Position = PlayerPosition.Bottom,
            Room = MapToResponse(room)
        };
    }

    public bool DeleteRoom(string roomId, string clientId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return false;

        // Only creator can delete, and only if game hasn't started
        if (room.CreatorClientId != clientId || room.Status != RoomStatus.Waiting)
            return false;

        return _roomRepository.Remove(roomId);
    }

    public JoinRoomResponse? JoinRoom(string roomId, JoinRoomRequest request)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null || room.Status != RoomStatus.Waiting)
            return null;

        var clientId = GenerateId("client");
        var client = new ConnectedClient
        {
            ClientId = clientId,
            DisplayName = request.DisplayName,
            IsPlayer = true
        };

        bool success;
        PlayerPosition? position;

        if (request.PreferredPosition.HasValue)
        {
            success = room.TryAddPlayerAtPosition(client, request.PreferredPosition.Value);
            position = success ? request.PreferredPosition : null;
        }
        else
        {
            success = room.TryAddPlayer(client, out position);
        }

        if (!success)
            return null;

        _roomRepository.Update(room);

        return new JoinRoomResponse
        {
            ClientId = clientId,
            Position = position,
            Room = MapToResponse(room)
        };
    }

    public JoinRoomResponse? WatchRoom(string roomId, JoinRoomRequest request)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return null;

        var clientId = GenerateId("client");
        var client = new ConnectedClient
        {
            ClientId = clientId,
            DisplayName = request.DisplayName,
            IsPlayer = false
        };

        room.Watchers.Add(client);
        _roomRepository.Update(room);

        return new JoinRoomResponse
        {
            ClientId = clientId,
            Position = null,
            Room = MapToResponse(room)
        };
    }

    public bool LeaveRoom(string roomId, string clientId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return false;

        // Try to remove as player first
        if (room.RemovePlayer(clientId))
        {
            _roomRepository.Update(room);
            return true;
        }

        // Try to remove as watcher
        var watcher = room.Watchers.FirstOrDefault(w => w.ClientId == clientId);
        if (watcher != null)
        {
            room.Watchers.Remove(watcher);
            _roomRepository.Update(room);
            return true;
        }

        return false;
    }

    public (StartGameResponse? Response, string? Error) StartGame(string roomId, string clientId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (null, "Room not found");

        if (room.Status != RoomStatus.Waiting)
            return (null, $"Room is not in waiting state (current: {room.Status})");

        // Only creator can start the game
        if (room.CreatorClientId != clientId)
            return (null, $"Only the room creator can start the game. Expected clientId starting with '{room.CreatorClientId[..Math.Min(12, room.CreatorClientId.Length)]}...', got '{clientId[..Math.Min(12, clientId.Length)]}...'");

        // Need at least 1 human player
        if (room.PlayerCount == 0)
            return (null, "No human players in the room");

        // Start the game (fills empty slots with AI)
        var gameSession = _gameService.CreateGame(room);
        if (gameSession == null)
            return (null, "Failed to create game session");

        room.Status = RoomStatus.Playing;
        room.GameSessionId = gameSession.GameId;
        _roomRepository.Update(room);

        // Notify all clients in the room that the game has started
        _ = _notifications.NotifyGameStartedAsync(roomId, gameSession.GameId);

        return (new StartGameResponse
        {
            GameId = gameSession.GameId,
            RoomId = roomId
        }, null);
    }

    public Room? GetRoomForClient(string clientId)
    {
        return _roomRepository.FindByClientId(clientId);
    }

    public void UpdateClientConnection(string clientId, string connectionId)
    {
        var room = _roomRepository.FindByClientId(clientId);
        if (room == null)
            return;

        var client = room.GetClient(clientId);
        if (client != null)
        {
            client.ConnectionId = connectionId;
            client.LastActivityAt = DateTime.UtcNow;
            _roomRepository.Update(room);
        }
    }

    private static string GenerateId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private static string GenerateRoomName(string creatorName)
    {
        var roomNumber = Interlocked.Increment(ref _roomCounter);
        return $"{creatorName}_#{roomNumber:D5}";
    }

    private static RoomResponse MapToResponse(Room room)
    {
        return new RoomResponse
        {
            RoomId = room.RoomId,
            Name = room.Name,
            Status = room.Status,
            PlayerCount = room.PlayerCount,
            WatcherCount = room.Watchers.Count,
            PlayerSlots = Enum.GetValues<PlayerPosition>()
                .Select(pos => new PlayerSlotResponse
                {
                    Position = pos,
                    IsOccupied = room.PlayerSlots[pos] != null || room.AiSlots.Contains(pos),
                    PlayerName = room.PlayerSlots[pos]?.DisplayName,
                    IsAi = room.AiSlots.Contains(pos)
                })
                .ToList(),
            GameId = room.GameSessionId,
            CreatedAt = room.CreatedAt
        };
    }
}
