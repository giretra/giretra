using Giretra.Web.Domain;

namespace Giretra.Web.Repositories;

/// <summary>
/// Repository for managing game rooms.
/// </summary>
public interface IRoomRepository
{
    /// <summary>
    /// Gets all rooms.
    /// </summary>
    IEnumerable<Room> GetAll();

    /// <summary>
    /// Gets a room by its ID.
    /// </summary>
    Room? GetById(string roomId);

    /// <summary>
    /// Adds a new room.
    /// </summary>
    void Add(Room room);

    /// <summary>
    /// Updates an existing room.
    /// </summary>
    void Update(Room room);

    /// <summary>
    /// Removes a room by its ID.
    /// </summary>
    bool Remove(string roomId);

    /// <summary>
    /// Finds a room containing a specific client.
    /// </summary>
    Room? FindByClientId(string clientId);

    /// <summary>
    /// Finds a room containing a client with the given SignalR connection ID.
    /// </summary>
    (Room Room, ConnectedClient Client)? FindByConnectionId(string connectionId);

    /// <summary>
    /// Counts active rooms owned by a specific user.
    /// </summary>
    int CountByOwner(Guid userId);
}
