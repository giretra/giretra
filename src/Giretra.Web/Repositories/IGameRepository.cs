using Giretra.Web.Domain;

namespace Giretra.Web.Repositories;

/// <summary>
/// Repository for managing game sessions.
/// </summary>
public interface IGameRepository
{
    /// <summary>
    /// Gets all game sessions.
    /// </summary>
    IEnumerable<GameSession> GetAll();

    /// <summary>
    /// Gets a game session by its ID.
    /// </summary>
    GameSession? GetById(string gameId);

    /// <summary>
    /// Gets a game session by room ID.
    /// </summary>
    GameSession? GetByRoomId(string roomId);

    /// <summary>
    /// Adds a new game session.
    /// </summary>
    void Add(GameSession session);

    /// <summary>
    /// Updates an existing game session.
    /// </summary>
    void Update(GameSession session);

    /// <summary>
    /// Removes a game session by its ID.
    /// </summary>
    bool Remove(string gameId);

    /// <summary>
    /// Finds a game session containing a specific client.
    /// </summary>
    GameSession? FindByClientId(string clientId);
}
