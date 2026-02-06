using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;

namespace Giretra.Web.Domain;

/// <summary>
/// Represents a pending action that the game is waiting for from a player.
/// </summary>
public sealed class PendingAction
{
    /// <summary>
    /// The type of action being awaited.
    /// </summary>
    public required PendingActionType ActionType { get; init; }

    /// <summary>
    /// The player who must take the action.
    /// </summary>
    public required PlayerPosition Player { get; init; }

    /// <summary>
    /// When the pending action was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// TaskCompletionSource for cut action (position, fromTop).
    /// </summary>
    public TaskCompletionSource<(int position, bool fromTop)>? CutTcs { get; init; }

    /// <summary>
    /// TaskCompletionSource for negotiation action.
    /// </summary>
    public TaskCompletionSource<NegotiationAction>? NegotiationTcs { get; init; }

    /// <summary>
    /// Valid negotiation actions available to the player.
    /// </summary>
    public IReadOnlyList<NegotiationAction>? ValidNegotiationActions { get; init; }

    /// <summary>
    /// TaskCompletionSource for card play action.
    /// </summary>
    public TaskCompletionSource<Card>? PlayCardTcs { get; init; }

    /// <summary>
    /// Valid cards the player can play.
    /// </summary>
    public IReadOnlyList<Card>? ValidCards { get; init; }

    /// <summary>
    /// TaskCompletionSource for continue deal confirmation.
    /// </summary>
    public TaskCompletionSource<bool>? ContinueDealTcs { get; init; }

    /// <summary>
    /// TaskCompletionSource for continue match confirmation.
    /// </summary>
    public TaskCompletionSource<bool>? ContinueMatchTcs { get; init; }

    /// <summary>
    /// The timeout duration for this action.
    /// </summary>
    public required TimeSpan TimeoutDuration { get; init; }

    /// <summary>
    /// Gets the timeout deadline.
    /// </summary>
    public DateTime TimeoutAt => CreatedAt + TimeoutDuration;

    /// <summary>
    /// Gets whether this action has timed out.
    /// </summary>
    public bool IsTimedOut => DateTime.UtcNow >= TimeoutAt;
}
