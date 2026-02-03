using Giretra.Core.Players;

namespace Giretra.Web.Domain;

/// <summary>
/// Represents a game room where players gather before starting a game.
/// </summary>
public sealed class Room
{
    /// <summary>
    /// Unique identifier for the room.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Display name for the room.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The client who created the room.
    /// </summary>
    public required string CreatorClientId { get; init; }

    /// <summary>
    /// Current status of the room.
    /// </summary>
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;

    /// <summary>
    /// Players in each position slot (null if empty).
    /// </summary>
    public Dictionary<PlayerPosition, ConnectedClient?> PlayerSlots { get; } = new()
    {
        [PlayerPosition.Bottom] = null,
        [PlayerPosition.Left] = null,
        [PlayerPosition.Top] = null,
        [PlayerPosition.Right] = null
    };

    /// <summary>
    /// Watchers observing the room.
    /// </summary>
    public List<ConnectedClient> Watchers { get; } = [];

    /// <summary>
    /// ID of the associated game session once started.
    /// </summary>
    public string? GameSessionId { get; set; }

    /// <summary>
    /// When the room was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the number of human players currently in the room.
    /// </summary>
    public int PlayerCount => PlayerSlots.Values.Count(p => p != null);

    /// <summary>
    /// Gets whether the room is full (4 players).
    /// </summary>
    public bool IsFull => PlayerCount == 4;

    /// <summary>
    /// Gets all connected clients (players and watchers).
    /// </summary>
    public IEnumerable<ConnectedClient> AllClients =>
        PlayerSlots.Values.Where(p => p != null).Cast<ConnectedClient>().Concat(Watchers);

    /// <summary>
    /// Tries to add a player to the first available slot.
    /// </summary>
    public bool TryAddPlayer(ConnectedClient client, out PlayerPosition? assignedPosition)
    {
        assignedPosition = null;

        if (Status != RoomStatus.Waiting)
            return false;

        // Find first empty slot
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            if (PlayerSlots[position] == null)
            {
                PlayerSlots[position] = client;
                client.Position = position;
                assignedPosition = position;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tries to add a player to a specific position.
    /// </summary>
    public bool TryAddPlayerAtPosition(ConnectedClient client, PlayerPosition position)
    {
        if (Status != RoomStatus.Waiting)
            return false;

        if (PlayerSlots[position] != null)
            return false;

        PlayerSlots[position] = client;
        client.Position = position;
        return true;
    }

    /// <summary>
    /// Removes a player from the room.
    /// </summary>
    public bool RemovePlayer(string clientId)
    {
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            if (PlayerSlots[position]?.ClientId == clientId)
            {
                PlayerSlots[position] = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a player by their client ID.
    /// </summary>
    public ConnectedClient? GetPlayer(string clientId)
    {
        return PlayerSlots.Values.FirstOrDefault(p => p?.ClientId == clientId);
    }

    /// <summary>
    /// Gets a client (player or watcher) by their client ID.
    /// </summary>
    public ConnectedClient? GetClient(string clientId)
    {
        return GetPlayer(clientId) ?? Watchers.FirstOrDefault(w => w.ClientId == clientId);
    }

    /// <summary>
    /// Gets the position for a player by client ID.
    /// </summary>
    public PlayerPosition? GetPlayerPosition(string clientId)
    {
        return PlayerSlots
            .Where(kvp => kvp.Value?.ClientId == clientId)
            .Select(kvp => (PlayerPosition?)kvp.Key)
            .FirstOrDefault();
    }
}
