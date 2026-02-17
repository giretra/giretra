using System.Collections.Concurrent;
using System.Security.Cryptography;
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
    private static readonly TimeSpan RoomCleanupDelay = TimeSpan.FromSeconds(20);

    private readonly IRoomRepository _roomRepository;
    private readonly IGameService _gameService;
    private readonly INotificationService _notifications;
    private readonly AiPlayerRegistry _aiRegistry;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRemovals = new();
    private static int _roomCounter;

    public RoomService(IRoomRepository roomRepository, IGameService gameService, INotificationService notifications, AiPlayerRegistry aiRegistry)
    {
        _roomRepository = roomRepository;
        _gameService = gameService;
        _notifications = notifications;
        _aiRegistry = aiRegistry;
    }

    public RoomListResponse GetAllRooms(Guid? requestingUserId = null)
    {
        var rooms = _roomRepository.GetAll().Select(r => MapToResponse(r, requestingUserId)).ToList();
        return new RoomListResponse
        {
            Rooms = rooms,
            TotalCount = rooms.Count
        };
    }

    public RoomResponse? GetRoom(string roomId, Guid? requestingUserId = null)
    {
        var room = _roomRepository.GetById(roomId);
        return room != null ? MapToResponse(room, requestingUserId) : null;
    }

    public JoinRoomResponse CreateRoom(CreateRoomRequest request, string displayName, Guid userId)
    {
        var roomId = GenerateId("room");
        var clientId = GenerateId("client");

        var creator = new ConnectedClient
        {
            UserId = userId,
            ClientId = clientId,
            DisplayName = displayName,
            IsPlayer = true,
            Position = PlayerPosition.Bottom
        };

        // Generate room name if not provided
        var roomName = string.IsNullOrWhiteSpace(request.Name)
            ? GenerateRoomName(displayName)
            : request.Name;

        var room = new Room
        {
            RoomId = roomId,
            Name = roomName,
            CreatorClientId = clientId,
            OwnerUserId = userId,
            TurnTimerSeconds = Math.Clamp(request.TurnTimerSeconds ?? 20, 5, 60)
        };

        room.PlayerSlots[PlayerPosition.Bottom] = creator;

        // Fill specified positions with AI
        if (request.AiSeats != null)
        {
            foreach (var seat in request.AiSeats)
            {
                // Only allow Left, Top, Right (Bottom is reserved for creator)
                if (seat.Position != PlayerPosition.Bottom)
                {
                    room.AiSlots[seat.Position] = seat.AiType;
                }
            }
        }

        // Set non-AI, non-Bottom seats to InviteOnly if requested
        if (request.InviteOnly)
        {
            foreach (var position in new[] { PlayerPosition.Left, PlayerPosition.Top, PlayerPosition.Right })
            {
                if (!room.AiSlots.ContainsKey(position))
                {
                    room.SeatConfigs[position].AccessMode = SeatAccessMode.InviteOnly;
                }
            }
        }

        _roomRepository.Add(room);

        return new JoinRoomResponse
        {
            ClientId = clientId,
            Position = PlayerPosition.Bottom,
            Room = MapToResponse(room, userId)
        };
    }

    public bool DeleteRoom(string roomId, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return false;

        // Only owner can delete, and only if game hasn't started
        if (!room.IsOwner(userId) || room.Status != RoomStatus.Waiting)
            return false;

        return _roomRepository.Remove(roomId);
    }

    public JoinRoomResponse? JoinRoom(string roomId, JoinRoomRequest request, string displayName, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null || room.Status != RoomStatus.Waiting)
            return null;

        var clientId = GenerateId("client");
        var client = new ConnectedClient
        {
            UserId = userId,
            ClientId = clientId,
            DisplayName = displayName,
            IsPlayer = true
        };

        bool success;
        PlayerPosition? position;

        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            // Invite token flow
            if (request.PreferredPosition.HasValue)
            {
                // Validate token against specific seat
                var seatConfig = room.SeatConfigs[request.PreferredPosition.Value];
                if (seatConfig.InviteToken != request.InviteToken)
                    return null;
                if (seatConfig.KickedUserIds.Contains(userId))
                    return null;

                success = room.TryAddPlayerAtPosition(client, request.PreferredPosition.Value);
                position = success ? request.PreferredPosition : null;

                if (success)
                    seatConfig.InviteToken = null; // Consume token
            }
            else
            {
                // Scan all seats for matching token
                success = false;
                position = null;

                foreach (var pos in Enum.GetValues<PlayerPosition>())
                {
                    var seatConfig = room.SeatConfigs[pos];
                    if (seatConfig.InviteToken == request.InviteToken
                        && !seatConfig.KickedUserIds.Contains(userId)
                        && room.PlayerSlots[pos] == null
                        && !room.AiSlots.ContainsKey(pos))
                    {
                        success = room.TryAddPlayerAtPosition(client, pos);
                        if (success)
                        {
                            position = pos;
                            seatConfig.InviteToken = null; // Consume token
                            break;
                        }
                    }
                }
            }
        }
        else if (request.PreferredPosition.HasValue)
        {
            var targetPos = request.PreferredPosition.Value;
            var seatConfig = room.SeatConfigs[targetPos];

            // Reject if kicked from this seat
            if (seatConfig.KickedUserIds.Contains(userId))
                return null;

            // Reject if invite-only and no token
            if (seatConfig.AccessMode == SeatAccessMode.InviteOnly)
                return null;

            success = room.TryAddPlayerAtPosition(client, targetPos);
            position = success ? request.PreferredPosition : null;
        }
        else
        {
            // Find first available public seat that user isn't kicked from
            success = false;
            position = null;

            foreach (var pos in Enum.GetValues<PlayerPosition>())
            {
                var seatConfig = room.SeatConfigs[pos];
                if (room.PlayerSlots[pos] == null
                    && !room.AiSlots.ContainsKey(pos)
                    && seatConfig.AccessMode == SeatAccessMode.Public
                    && !seatConfig.KickedUserIds.Contains(userId))
                {
                    success = room.TryAddPlayerAtPosition(client, pos);
                    if (success)
                    {
                        position = pos;
                        break;
                    }
                }
            }
        }

        if (!success)
            return null;

        _roomRepository.Update(room);

        return new JoinRoomResponse
        {
            ClientId = clientId,
            Position = position,
            Room = MapToResponse(room, userId)
        };
    }

    public JoinRoomResponse? WatchRoom(string roomId, JoinRoomRequest request, string displayName)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return null;

        var clientId = GenerateId("client");
        var client = new ConnectedClient
        {
            ClientId = clientId,
            DisplayName = displayName,
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

    public (bool Removed, string? PlayerName, PlayerPosition? Position) LeaveRoom(string roomId, string clientId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (false, null, null);

        var removed = false;
        string? playerName = null;
        PlayerPosition? position = null;

        // Try to remove as player first — capture info before removal
        var player = room.GetPlayer(clientId);
        if (player != null)
        {
            playerName = player.DisplayName;
            position = room.GetPlayerPosition(clientId);
            room.RemovePlayer(clientId);
            removed = true;
        }
        else
        {
            // Try to remove as watcher
            var watcher = room.Watchers.FirstOrDefault(w => w.ClientId == clientId);
            if (watcher != null)
            {
                room.Watchers.Remove(watcher);
                removed = true;
            }
        }

        if (!removed)
            return (false, null, null);

        // Remove the room if no one is left
        if (room.IsEmpty)
            _roomRepository.Remove(roomId);
        else
            _roomRepository.Update(room);

        return (true, playerName, position);
    }

    public (StartGameResponse? Response, string? Error) StartGame(string roomId, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (null, "Room not found");

        if (room.Status != RoomStatus.Waiting)
            return (null, $"Room is not in waiting state (current: {room.Status})");

        // Only owner can start the game
        if (!room.IsOwner(userId))
            return (null, "Only the room owner can start the game");

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

    public (bool Success, string? Error) SetSeatMode(string roomId, Guid userId, PlayerPosition position, SeatAccessMode mode)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (false, "Room not found");

        if (!room.IsOwner(userId))
            return (false, "Only the room owner can change seat modes");

        if (room.Status != RoomStatus.Waiting)
            return (false, "Can only change seat modes while waiting");

        if (position == PlayerPosition.Bottom)
            return (false, "Cannot change the owner's seat mode");

        var seatConfig = room.SeatConfigs[position];
        seatConfig.AccessMode = mode;

        // Clear invite token if switching to Public
        if (mode == SeatAccessMode.Public)
            seatConfig.InviteToken = null;

        _roomRepository.Update(room);
        return (true, null);
    }

    public InviteTokenResponse? GenerateInviteToken(string roomId, Guid userId, PlayerPosition position, string baseUrl)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null || !room.IsOwner(userId) || room.Status != RoomStatus.Waiting)
            return null;

        if (position == PlayerPosition.Bottom)
            return null;

        var seatConfig = room.SeatConfigs[position];

        // Auto-set to InviteOnly
        seatConfig.AccessMode = SeatAccessMode.InviteOnly;

        // Generate 16-char hex token
        var tokenBytes = RandomNumberGenerator.GetBytes(8);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();
        seatConfig.InviteToken = token;

        _roomRepository.Update(room);

        var inviteUrl = $"{baseUrl.TrimEnd('/')}/table/{roomId}?invite={token}";

        return new InviteTokenResponse
        {
            Position = position,
            Token = token,
            InviteUrl = inviteUrl
        };
    }

    public (bool Success, string? Error, PlayerPosition? Position, string? PlayerName) KickPlayer(string roomId, Guid userId, PlayerPosition position)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (false, "Room not found", null, null);

        if (!room.IsOwner(userId))
            return (false, "Only the room owner can kick players", null, null);

        if (room.Status != RoomStatus.Waiting)
            return (false, "Can only kick players while waiting", null, null);

        if (position == PlayerPosition.Bottom)
            return (false, "Cannot kick yourself", null, null);

        var player = room.PlayerSlots[position];
        if (player == null)
            return (false, "No player in that seat", null, null);

        var playerName = player.DisplayName;

        // Add to kick list if they have a persistent user ID
        if (player.UserId.HasValue)
            room.SeatConfigs[position].KickedUserIds.Add(player.UserId.Value);

        // Remove from slot
        room.PlayerSlots[position] = null;
        _roomRepository.Update(room);

        return (true, null, position, playerName);
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

            // Cancel any pending cleanup for this client (reconnection)
            CancelPendingRemoval(room.RoomId, clientId);
        }
    }

    public void HandleDisconnect(string connectionId)
    {
        var result = _roomRepository.FindByConnectionId(connectionId);
        if (result == null)
            return;

        var (room, client) = result.Value;

        // Clear connection but keep the client in the room for a grace period
        client.ConnectionId = null;
        _roomRepository.Update(room);

        // Schedule delayed removal
        ScheduleDelayedRemoval(room.RoomId, client.ClientId);
    }

    private void ScheduleDelayedRemoval(string roomId, string clientId)
    {
        var key = $"{roomId}_{clientId}";

        // Cancel any existing timer for this client
        CancelPendingRemoval(roomId, clientId);

        var cts = new CancellationTokenSource();
        _pendingRemovals[key] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RoomCleanupDelay, cts.Token);
                _pendingRemovals.TryRemove(key, out _);
                cts.Dispose();

                // Client did not reconnect in time — actually remove them
                var (removed, playerName, position) = LeaveRoom(roomId, clientId);
                if (removed && playerName != null && position.HasValue)
                    await _notifications.NotifyPlayerLeftAsync(roomId, playerName, position.Value);
            }
            catch (OperationCanceledException)
            {
                // Client reconnected, removal cancelled
            }
        });
    }

    private void CancelPendingRemoval(string roomId, string clientId)
    {
        var key = $"{roomId}_{clientId}";
        if (_pendingRemovals.TryRemove(key, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
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

    private RoomResponse MapToResponse(Room room, Guid? requestingUserId = null)
    {
        var isOwner = requestingUserId.HasValue && room.IsOwner(requestingUserId.Value);

        return new RoomResponse
        {
            RoomId = room.RoomId,
            Name = room.Name,
            Status = room.Status,
            PlayerCount = room.PlayerCount,
            WatcherCount = room.Watchers.Count,
            PlayerSlots = Enum.GetValues<PlayerPosition>()
                .Select(pos =>
                {
                    var seatConfig = room.SeatConfigs[pos];
                    return new PlayerSlotResponse
                    {
                        Position = pos,
                        IsOccupied = room.PlayerSlots[pos] != null || room.AiSlots.ContainsKey(pos),
                        PlayerName = room.PlayerSlots[pos]?.DisplayName,
                        IsAi = room.AiSlots.ContainsKey(pos),
                        AiType = room.AiSlots.GetValueOrDefault(pos),
                        AiDisplayName = room.AiSlots.TryGetValue(pos, out var aiType) ? _aiRegistry.GetDisplayName(aiType) : null,
                        AccessMode = seatConfig.AccessMode,
                        HasInvite = isOwner && seatConfig.InviteToken != null
                    };
                })
                .ToList(),
            GameId = room.GameSessionId,
            CreatedAt = room.CreatedAt,
            TurnTimerSeconds = room.TurnTimerSeconds,
            IsOwner = isOwner
        };
    }
}
