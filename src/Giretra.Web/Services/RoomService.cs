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
    private static readonly TimeSpan RoomIdleTimeout = TimeSpan.FromMinutes(2);

    private readonly IRoomRepository _roomRepository;
    private readonly IGameService _gameService;
    private readonly INotificationService _notifications;
    private readonly AiPlayerRegistry _aiRegistry;
    private readonly ILogger<RoomService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRemovals = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingIdleCleanups = new();
    private static int _roomCounter;

    public RoomService(IRoomRepository roomRepository, IGameService gameService, INotificationService notifications, AiPlayerRegistry aiRegistry, ILogger<RoomService> logger)
    {
        _roomRepository = roomRepository;
        _gameService = gameService;
        _notifications = notifications;
        _aiRegistry = aiRegistry;
        _logger = logger;
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

    public (JoinRoomResponse? Response, string? Error) CreateRoom(CreateRoomRequest request, string displayName, Guid userId)
    {
        if (_roomRepository.CountByOwner(userId) >= 2)
            return (null, "You can have at most 2 active tables");

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
            TurnTimerSeconds = Math.Clamp(request.TurnTimerSeconds ?? 20, 5, 60),
            IsRanked = request.IsRanked
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
        ScheduleIdleCleanup(roomId);

        return (new JoinRoomResponse
        {
            ClientId = clientId,
            Position = PlayerPosition.Bottom,
            Room = MapToResponse(room, userId)
        }, null);
    }

    public bool DeleteRoom(string roomId, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return false;

        // Only owner can delete, and only if game hasn't started
        if (!room.IsOwner(userId) || room.Status != RoomStatus.Waiting)
            return false;

        CancelIdleCleanup(roomId);
        return _roomRepository.Remove(roomId);
    }

    public (JoinRoomResponse? Response, string? Error) JoinRoom(string roomId, JoinRoomRequest request, string displayName, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (null, "Room not found");
        if (room.Status != RoomStatus.Waiting)
            return (null, "Room is not in waiting state");

        if (room.HasPlayer(userId))
            return (null, "You are already seated in this room");

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
                    return (null, "Invalid invite token");
                if (seatConfig.KickedUserIds.Contains(userId))
                    return (null, "You have been kicked from this seat");

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
                return (null, "You have been kicked from this seat");

            // Reject if invite-only and no token
            if (seatConfig.AccessMode == SeatAccessMode.InviteOnly)
                return (null, "This seat is invite-only");

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
            return (null, "Unable to join room. Room may be full or seat is unavailable.");

        _roomRepository.Update(room);

        return (new JoinRoomResponse
        {
            ClientId = clientId,
            Position = position,
            Room = MapToResponse(room, userId)
        }, null);
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

        // Cancel idle timeout since the game is starting
        CancelIdleCleanup(roomId);

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

    public InviteTokenResponse? GenerateInviteToken(string roomId, Guid userId, PlayerPosition position)
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

        return new InviteTokenResponse
        {
            Position = position,
            Token = token
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

    public (JoinRoomResponse? Response, string? Error) RejoinRoom(string roomId, string displayName, Guid userId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
            return (null, "Room not found");

        if (room.Status != RoomStatus.Playing)
            return (null, "Room is not in playing state");

        if (room.GameSessionId == null)
            return (null, "No active game session");

        // Case (a): Player still seated (client in PlayerSlots with matching userId, possibly null ConnectionId)
        var existingEntry = room.PlayerSlots
            .FirstOrDefault(kvp => kvp.Value?.UserId == userId);

        if (existingEntry.Value != null)
        {
            var existingClient = existingEntry.Value;
            var position = existingEntry.Key;

            if (existingClient.ConnectionId != null)
                return (null, "Player is already connected");

            // Player still has a client slot — reuse the existing clientId
            _logger.LogInformation(
                "Player {UserId} rejoining room {RoomId} at position {Position} (reusing clientId {ClientId})",
                userId, roomId, position, existingClient.ClientId);

            // Cancel any pending removal for this client
            CancelPendingRemoval(roomId, existingClient.ClientId);

            return (new JoinRoomResponse
            {
                ClientId = existingClient.ClientId,
                Position = position,
                Room = MapToResponse(room, userId)
            }, null);
        }

        // Case (b): Player in DisconnectedPlayers (client was cleaned up but position preserved)
        var disconnectedEntry = room.DisconnectedPlayers
            .FirstOrDefault(kvp => kvp.Value == userId);

        if (disconnectedEntry.Value == userId && room.DisconnectedPlayers.ContainsKey(disconnectedEntry.Key))
        {
            var position = disconnectedEntry.Key;
            var newClientId = GenerateId("client");

            var newClient = new ConnectedClient
            {
                UserId = userId,
                ClientId = newClientId,
                DisplayName = displayName,
                IsPlayer = true,
                Position = position
            };

            // Place in the stored position
            room.PlayerSlots[position] = newClient;
            room.DisconnectedPlayers.Remove(position);

            // Remap in the game session
            // We need the old clientId — we don't have it anymore in DisconnectedPlayers,
            // so we need to search the game session for this position
            var game = _gameService.GetGame(room.GameSessionId);
            if (game != null)
            {
                var oldClientId = game.ClientPositions
                    .FirstOrDefault(kvp => kvp.Value == position).Key;

                if (oldClientId != null)
                {
                    _gameService.RejoinPlayer(
                        room.GameSessionId, oldClientId, newClientId,
                        TimeSpan.FromSeconds(room.TurnTimerSeconds));
                }
            }

            _roomRepository.Update(room);

            _logger.LogInformation(
                "Player {UserId} rejoined room {RoomId} at position {Position} with new clientId {ClientId}",
                userId, roomId, position, newClientId);

            return (new JoinRoomResponse
            {
                ClientId = newClientId,
                Position = position,
                Room = MapToResponse(room, userId)
            }, null);
        }

        return (null, "You are not a disconnected player in this room");
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

    public void ResetToWaiting(string roomId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null) return;

        room.Status = RoomStatus.Waiting;
        room.GameSessionId = null;
        _roomRepository.Update(room);
        _logger.LogInformation("Room {RoomId} reset to Waiting state", roomId);

        ScheduleIdleCleanup(roomId);
    }

    private void ScheduleIdleCleanup(string roomId)
    {
        CancelIdleCleanup(roomId);

        var room = _roomRepository.GetById(roomId);
        if (room == null) return;

        room.IdleDeadline = DateTime.UtcNow.Add(RoomIdleTimeout);
        _roomRepository.Update(room);

        var cts = new CancellationTokenSource();
        _pendingIdleCleanups[roomId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RoomIdleTimeout, cts.Token);
                _pendingIdleCleanups.TryRemove(roomId, out _);
                cts.Dispose();

                var roomToClose = _roomRepository.GetById(roomId);
                if (roomToClose == null || roomToClose.Status != RoomStatus.Waiting)
                    return;

                _logger.LogInformation("Room {RoomId} idle timeout expired, closing room", roomId);

                // Notify all clients before removing them
                await _notifications.NotifyRoomIdleClosedAsync(roomId);

                // Leave all players and watchers
                var allClientIds = roomToClose.AllClients.Select(c => c.ClientId).ToList();
                foreach (var clientId in allClientIds)
                {
                    LeaveRoom(roomId, clientId);
                }

                // Delete the room if it still exists
                _roomRepository.Remove(roomId);
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled (game started or room manually closed)
            }
        });
    }

    private void CancelIdleCleanup(string roomId)
    {
        if (_pendingIdleCleanups.TryRemove(roomId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        var room = _roomRepository.GetById(roomId);
        if (room != null)
        {
            room.IdleDeadline = null;
            _roomRepository.Update(room);
        }
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

                var room = _roomRepository.GetById(roomId);
                if (room == null)
                    return;

                if (room.Status == RoomStatus.Playing && room.GameSessionId != null)
                {
                    // Game is active — do NOT abandon or remove the player.
                    // Their WebApiPlayerAgent handles turns via timeout (auto-plays defaults).
                    // The player can rejoin later via the rejoin endpoint.
                    _logger.LogInformation(
                        "Player {ClientId} disconnected during active game {GameId}, keeping seat for rejoin",
                        clientId, room.GameSessionId);
                    return;
                }

                // Room is Waiting — remove the player normally
                var (removed, playerName, leftPosition) = LeaveRoom(roomId, clientId);
                if (removed && playerName != null && leftPosition.HasValue)
                    await _notifications.NotifyPlayerLeftAsync(roomId, playerName, leftPosition.Value);
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

        // Check if the requesting user is a disconnected player in this Playing room
        var isDisconnectedPlayer = false;
        if (requestingUserId.HasValue && room.Status == RoomStatus.Playing)
        {
            // Check if seated but with null ConnectionId
            isDisconnectedPlayer = room.PlayerSlots.Values
                .Any(p => p?.UserId == requestingUserId.Value && p.ConnectionId == null);

            // Also check DisconnectedPlayers dict
            if (!isDisconnectedPlayer)
            {
                isDisconnectedPlayer = room.DisconnectedPlayers.Values
                    .Any(uid => uid == requestingUserId.Value);
            }
        }

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
                        HasInvite = isOwner && seatConfig.InviteToken != null,
                        IsCurrentUser = requestingUserId.HasValue && room.PlayerSlots[pos]?.UserId == requestingUserId.Value
                    };
                })
                .ToList(),
            GameId = room.GameSessionId,
            CreatedAt = room.CreatedAt,
            TurnTimerSeconds = room.TurnTimerSeconds,
            IsOwner = isOwner,
            IsRanked = room.IsRanked,
            IsDisconnectedPlayer = isDisconnectedPlayer,
            IdleDeadline = room.IdleDeadline
        };
    }
}
