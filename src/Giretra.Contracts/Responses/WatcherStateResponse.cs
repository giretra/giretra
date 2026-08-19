using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for watcher-specific game state (hides player hands).
/// </summary>
public sealed class WatcherStateResponse
{
    /// <summary>
    /// The full game state (public information).
    /// </summary>
    public required GameStateResponse GameState { get; init; }

    /// <summary>
    /// Number of cards each player has remaining.
    /// </summary>
    public required IReadOnlyDictionary<PlayerPosition, int> PlayerCardCounts { get; init; }
}
