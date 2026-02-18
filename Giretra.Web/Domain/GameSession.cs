using Giretra.Core;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Players;

namespace Giretra.Web.Domain;

/// <summary>
/// Represents an active game session.
/// </summary>
public sealed class GameSession
{
    /// <summary>
    /// Unique identifier for this game session.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The room this game was started from.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Player agents for each position.
    /// </summary>
    public IReadOnlyDictionary<PlayerPosition, IPlayerAgent> PlayerAgents { get; set; } = null!;

    /// <summary>
    /// Mapping of client IDs to player positions (for human players only).
    /// </summary>
    public required IReadOnlyDictionary<string, PlayerPosition> ClientPositions { get; init; }

    /// <summary>
    /// Metadata about each player (human vs bot, user IDs, agent types) for Elo computation.
    /// </summary>
    public required IReadOnlyDictionary<PlayerPosition, MatchPlayerInfo> PlayerComposition { get; init; }

    /// <summary>
    /// The game manager running the match.
    /// </summary>
    public GameManager? GameManager { get; set; }

    /// <summary>
    /// The current pending action, if any.
    /// </summary>
    public PendingAction? PendingAction { get; set; }

    /// <summary>
    /// The background task running the game loop.
    /// </summary>
    public Task? GameLoopTask { get; set; }

    /// <summary>
    /// Cancellation token source for stopping the game.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <summary>
    /// Records all player actions for persistence.
    /// </summary>
    public ActionRecorder? ActionRecorder { get; set; }

    /// <summary>
    /// When the game started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the game completed (null if still running).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets the current match state.
    /// </summary>
    public MatchState? MatchState => GameManager?.MatchState;

    /// <summary>
    /// Gets whether the game is complete.
    /// </summary>
    public bool IsComplete => MatchState?.IsComplete ?? false;

    /// <summary>
    /// Gets the player position for a client ID.
    /// </summary>
    public PlayerPosition? GetPositionForClient(string clientId)
    {
        return ClientPositions.TryGetValue(clientId, out var position) ? position : null;
    }

    /// <summary>
    /// Gets whether a client is playing in this game.
    /// </summary>
    public bool IsClientPlaying(string clientId)
    {
        return ClientPositions.ContainsKey(clientId);
    }
}
