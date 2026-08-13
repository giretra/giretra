using Giretra.Core.Cards;
using Giretra.Core.State;

namespace Giretra.Core.Players.Agents;

/// <summary>
/// Tracks the order of the deck from one deal to the next.
/// <para>
/// The deck is never shuffled during a match: after each hand it is rebuilt from the completed
/// tricks, with the dealer team's tricks on top of the other team's. Every player sees all 32
/// cards of every trick, so a player who remembers them knows the exact order of the deck the
/// next deal will be dealt from — which is what makes an informed cut possible.
/// </para>
/// <para>
/// Feed this from the observation callbacks of <see cref="IPlayerAgent"/>:
/// <see cref="OnDealStarted"/> from <c>OnDealStartedAsync</c>, <see cref="OnDealEnded"/> from
/// <c>OnDealEndedAsync</c> and <see cref="Reset"/> from <c>OnMatchEndedAsync</c>.
/// </para>
/// </summary>
public sealed class DeckTracker
{
    private PlayerPosition? _dealer;

    /// <summary>
    /// Gets the order of the deck the next deal will use, or null when it is unknown
    /// (before the first deal of a match completes, or after the prediction was invalidated).
    /// </summary>
    public Deck? PredictedDeck { get; private set; }

    /// <summary>
    /// Gets whether a full 32-card deck order is known.
    /// </summary>
    public bool IsReliable => PredictedDeck is { Count: 32 };

    /// <summary>
    /// Records the dealer of the deal that is starting. The dealer decides which team's tricks
    /// end up on top when the deck is rebuilt, and the match state has already rotated to the
    /// next dealer by the time the deal ends, so it has to be captured here.
    /// </summary>
    public void OnDealStarted(MatchState matchState)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        _dealer = matchState.CurrentDeal?.Dealer ?? matchState.CurrentDealer;
    }

    /// <summary>
    /// Rebuilds the predicted deck from the tricks of the deal that just ended.
    /// </summary>
    public void OnDealEnded(HandState handState)
    {
        ArgumentNullException.ThrowIfNull(handState);

        if (_dealer is null || !handState.IsComplete)
        {
            Invalidate();
            return;
        }

        PredictedDeck = handState.CollectDeck(_dealer.Value.GetTeam());
    }

    /// <summary>
    /// Drops the prediction, after which cuts fall back to a fixed position until the next
    /// deal ends and the order can be observed again from scratch.
    /// </summary>
    public void Invalidate() => PredictedDeck = null;

    /// <summary>
    /// Clears all tracking. Call this between matches, since a fresh match starts from a
    /// shuffled deck.
    /// </summary>
    public void Reset()
    {
        PredictedDeck = null;
        _dealer = null;
    }
}
