using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

/// <summary>
/// Service for managing game rooms.
/// </summary>
public interface IRoomService
{
    /// <summary>
    /// Gets all rooms.
    /// </summary>
    RoomListResponse GetAllRooms(Guid? requestingUserId = null);

    /// <summary>
    /// Gets a room by ID.
    /// </summary>
    RoomResponse? GetRoom(string roomId, Guid? requestingUserId = null);

    /// <summary>
    /// Creates a new room.
    /// </summary>
    /// <returns>A tuple containing the response (or null on failure) and an error message.</returns>
    (JoinRoomResponse? Response, string? Error) CreateRoom(CreateRoomRequest request, string displayName, Guid userId);

    /// <summary>
    /// Deletes a room.
    /// </summary>
    bool DeleteRoom(string roomId, Guid userId);

    /// <summary>
    /// Joins a room as a player.
    /// </summary>
    /// <returns>A tuple containing the response (or null on failure) and an error message.</returns>
    (JoinRoomResponse? Response, string? Error) JoinRoom(string roomId, JoinRoomRequest request, string displayName, Guid userId);

    /// <summary>
    /// Joins a room as a watcher.
    /// </summary>
    JoinRoomResponse? WatchRoom(string roomId, JoinRoomRequest request, string displayName);

    /// <summary>
    /// Leaves a room. Returns the removed player's name and position if they were a player.
    /// </summary>
    (bool Removed, string? PlayerName, PlayerPosition? Position) LeaveRoom(string roomId, string clientId);

    /// <summary>
    /// Starts the game in a room.
    /// </summary>
    /// <returns>A tuple containing the response (or null on failure) and an error message.</returns>
    (StartGameResponse? Response, string? Error) StartGame(string roomId, Guid userId);

    /// <summary>
    /// Gets the room a client is in.
    /// </summary>
    Room? GetRoomForClient(string clientId);

    /// <summary>
    /// Updates a client's SignalR connection ID.
    /// </summary>
    void UpdateClientConnection(string clientId, string connectionId);

    /// <summary>
    /// Handles a client disconnecting by connection ID.
    /// Removes the client from their room and deletes the room if empty.
    /// </summary>
    void HandleDisconnect(string connectionId);

    /// <summary>
    /// Sets the access mode for a seat. Owner-only, Waiting-only.
    /// </summary>
    (bool Success, string? Error) SetSeatMode(string roomId, Guid userId, PlayerPosition position, SeatAccessMode mode);

    /// <summary>
    /// Generates an invite token for a seat. Auto-sets seat to InviteOnly.
    /// </summary>
    InviteTokenResponse? GenerateInviteToken(string roomId, Guid userId, PlayerPosition position, string baseUrl);

    /// <summary>
    /// Kicks a player from a seat. Owner-only, Waiting-only.
    /// </summary>
    (bool Success, string? Error, PlayerPosition? Position, string? PlayerName) KickPlayer(string roomId, Guid userId, PlayerPosition position);

    /// <summary>
    /// Allows a disconnected player to rejoin an active game by userId.
    /// </summary>
    (JoinRoomResponse? Response, string? Error) RejoinRoom(string roomId, string displayName, Guid userId);
}
