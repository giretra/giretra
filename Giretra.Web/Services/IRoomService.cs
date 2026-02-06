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
    RoomListResponse GetAllRooms();

    /// <summary>
    /// Gets a room by ID.
    /// </summary>
    RoomResponse? GetRoom(string roomId);

    /// <summary>
    /// Creates a new room.
    /// </summary>
    JoinRoomResponse CreateRoom(CreateRoomRequest request);

    /// <summary>
    /// Deletes a room.
    /// </summary>
    bool DeleteRoom(string roomId, string clientId);

    /// <summary>
    /// Joins a room as a player.
    /// </summary>
    JoinRoomResponse? JoinRoom(string roomId, JoinRoomRequest request);

    /// <summary>
    /// Joins a room as a watcher.
    /// </summary>
    JoinRoomResponse? WatchRoom(string roomId, JoinRoomRequest request);

    /// <summary>
    /// Leaves a room. Returns the removed player's name and position if they were a player.
    /// </summary>
    (bool Removed, string? PlayerName, PlayerPosition? Position) LeaveRoom(string roomId, string clientId);

    /// <summary>
    /// Starts the game in a room.
    /// </summary>
    /// <returns>A tuple containing the response (or null on failure) and an error message.</returns>
    (StartGameResponse? Response, string? Error) StartGame(string roomId, string clientId);

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
}
