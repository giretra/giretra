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
    /// Persistent user identity of the room owner (survives reconnections).
    /// </summary>
    public required Guid OwnerUserId { get; init; }

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
    /// Positions reserved for AI players, mapped to AI type name.
    /// </summary>
    public Dictionary<PlayerPosition, string> AiSlots { get; } = [];

    /// <summary>
    /// Per-seat access configuration (public/invite-only, kick list).
    /// </summary>
    public Dictionary<PlayerPosition, SeatConfig> SeatConfigs { get; } = new()
    {
        [PlayerPosition.Bottom] = new SeatConfig(),
        [PlayerPosition.Left] = new SeatConfig(),
        [PlayerPosition.Top] = new SeatConfig(),
        [PlayerPosition.Right] = new SeatConfig()
    };

    /// <summary>
    /// Tracks userId → position for players who disconnected during a Playing game
    /// and whose client entry was cleaned up. Used for userId-based rejoin.
    /// </summary>
    public Dictionary<PlayerPosition, Guid> DisconnectedPlayers { get; } = [];

    /// <summary>
    /// Watchers observing the room.
    /// </summary>
    public List<ConnectedClient> Watchers { get; } = [];

    /// <summary>
    /// ID of the associated game session once started.
    /// </summary>
    public string? GameSessionId { get; set; }

    /// <summary>
    /// Whether games in this room affect player ratings.
    /// </summary>
    public bool IsRanked { get; init; } = true;

    /// <summary>
    /// Turn timer duration in seconds (5–60).
    /// </summary>
    public int TurnTimerSeconds { get; init; } = 20;

    /// <summary>
    /// When the room was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the room will be auto-closed due to idle timeout (null if no timeout active).
    /// </summary>
    public DateTime? IdleDeadline { get; set; }

    /// <summary>
    /// Gets the number of human players currently in the room.
    /// </summary>
    public int PlayerCount => PlayerSlots.Values.Count(p => p != null);

    /// <summary>
    /// Gets whether the room is full (4 human players or human + AI).
    /// </summary>
    public bool IsFull => PlayerCount + AiSlots.Count == 4;

    /// <summary>
    /// Gets whether the room has no human players and no watchers.
    /// </summary>
    public bool IsEmpty => PlayerCount == 0 && Watchers.Count == 0;

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

        // Find first empty slot (not occupied by human and not reserved for AI)
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            if (PlayerSlots[position] == null && !AiSlots.ContainsKey(position))
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
    /// Checks whether a user is already seated in the room.
    /// </summary>
    public bool HasPlayer(Guid userId)
    {
        return PlayerSlots.Values.Any(p => p?.UserId == userId);
    }

    /// <summary>
    /// Tries to add a player to a specific position.
    /// </summary>
    public bool TryAddPlayerAtPosition(ConnectedClient client, PlayerPosition position)
    {
        if (Status != RoomStatus.Waiting)
            return false;

        // Cannot join if already seated in this room
        if (client.UserId.HasValue && HasPlayer(client.UserId.Value))
            return false;

        // Cannot join if slot is occupied by human or reserved for AI
        if (PlayerSlots[position] != null || AiSlots.ContainsKey(position))
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

    /// <summary>
    /// Checks if the given user is the room owner.
    /// </summary>
    public bool IsOwner(Guid userId) => OwnerUserId == userId;
}
