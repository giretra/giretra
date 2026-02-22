using System.Collections.Concurrent;
using Giretra.Web.Domain;

namespace Giretra.Web.Repositories;

/// <summary>
/// In-memory implementation of the game repository.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, GameSession> _games = new();

    public IEnumerable<GameSession> GetAll()
    {
        return _games.Values.ToList();
    }

    public GameSession? GetById(string gameId)
    {
        return _games.TryGetValue(gameId, out var game) ? game : null;
    }

    public GameSession? GetByRoomId(string roomId)
    {
        return _games.Values.FirstOrDefault(g => g.RoomId == roomId);
    }

    public void Add(GameSession session)
    {
        if (!_games.TryAdd(session.GameId, session))
        {
            throw new InvalidOperationException($"Game with ID {session.GameId} already exists.");
        }
    }

    public void Update(GameSession session)
    {
        _games[session.GameId] = session;
    }

    public bool Remove(string gameId)
    {
        return _games.TryRemove(gameId, out _);
    }

    public GameSession? FindByClientId(string clientId)
    {
        return _games.Values.FirstOrDefault(g => g.IsClientPlaying(clientId));
    }
}
