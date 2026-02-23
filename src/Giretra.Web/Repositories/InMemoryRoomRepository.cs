using System.Collections.Concurrent;
using Giretra.Web.Domain;

namespace Giretra.Web.Repositories;

/// <summary>
/// In-memory implementation of the room repository.
/// </summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();

    public IEnumerable<Room> GetAll()
    {
        return _rooms.Values.ToList();
    }

    public Room? GetById(string roomId)
    {
        return _rooms.TryGetValue(roomId, out var room) ? room : null;
    }

    public void Add(Room room)
    {
        if (!_rooms.TryAdd(room.RoomId, room))
        {
            throw new InvalidOperationException($"Room with ID {room.RoomId} already exists.");
        }
    }

    public void Update(Room room)
    {
        _rooms[room.RoomId] = room;
    }

    public bool Remove(string roomId)
    {
        return _rooms.TryRemove(roomId, out _);
    }

    public Room? FindByClientId(string clientId)
    {
        return _rooms.Values.FirstOrDefault(r => r.GetClient(clientId) != null);
    }

    public (Room Room, ConnectedClient Client)? FindByConnectionId(string connectionId)
    {
        foreach (var room in _rooms.Values)
        {
            var client = room.AllClients.FirstOrDefault(c => c.ConnectionId == connectionId);
            if (client != null)
                return (room, client);
        }

        return null;
    }

    public int CountByOwner(Guid userId)
        => _rooms.Values.Count(r => r.OwnerUserId == userId);
}
