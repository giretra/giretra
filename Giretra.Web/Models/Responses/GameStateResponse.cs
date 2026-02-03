using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Domain;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for the full game state.
/// </summary>
public sealed class GameStateResponse
{
    /// <summary>
    /// The game session ID.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// The room ID.
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// Target score to win the match.
    /// </summary>
    public required int TargetScore { get; init; }

    /// <summary>
    /// Team 1 (Bottom+Top) match points.
    /// </summary>
    public required int Team1MatchPoints { get; init; }

    /// <summary>
    /// Team 2 (Left+Right) match points.
    /// </summary>
    public required int Team2MatchPoints { get; init; }

    /// <summary>
    /// Current dealer position.
    /// </summary>
    public required PlayerPosition Dealer { get; init; }

    /// <summary>
    /// Current phase of the deal.
    /// </summary>
    public required DealPhase Phase { get; init; }

    /// <summary>
    /// Number of completed deals.
    /// </summary>
    public required int CompletedDealsCount { get; init; }

    /// <summary>
    /// The resolved game mode for the current deal (null during negotiation).
    /// </summary>
    public GameMode? GameMode { get; init; }

    /// <summary>
    /// The multiplier state for the current deal.
    /// </summary>
    public MultiplierState? Multiplier { get; init; }

    /// <summary>
    /// Current trick state (if in playing phase).
    /// </summary>
    public TrickResponse? CurrentTrick { get; init; }

    /// <summary>
    /// Completed tricks in the current hand.
    /// </summary>
    public IReadOnlyList<TrickResponse>? CompletedTricks { get; init; }

    /// <summary>
    /// Team 1 card points in current hand.
    /// </summary>
    public int? Team1CardPoints { get; init; }

    /// <summary>
    /// Team 2 card points in current hand.
    /// </summary>
    public int? Team2CardPoints { get; init; }

    /// <summary>
    /// Negotiation history.
    /// </summary>
    public IReadOnlyList<NegotiationActionResponse>? NegotiationHistory { get; init; }

    /// <summary>
    /// The current bid (during negotiation).
    /// </summary>
    public GameMode? CurrentBid { get; init; }

    /// <summary>
    /// Whether the match is complete.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// The winning team (null if match not complete).
    /// </summary>
    public Team? Winner { get; init; }

    /// <summary>
    /// Type of pending action (if any).
    /// </summary>
    public PendingActionType? PendingActionType { get; init; }

    /// <summary>
    /// Player who must act (if any).
    /// </summary>
    public PlayerPosition? PendingActionPlayer { get; init; }
}
