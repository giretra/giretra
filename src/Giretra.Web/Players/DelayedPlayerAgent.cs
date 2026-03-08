using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Web.Players;

/// <summary>
/// Decorator that adds an artificial delay before AI agent decisions,
/// so human players can follow the game at a comfortable pace.
/// </summary>
public sealed class DelayedPlayerAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;
    private readonly TimeSpan _delay;

    public DelayedPlayerAgent(IPlayerAgent inner, TimeSpan delay)
    {
        _inner = inner;
        _delay = delay;
    }

    public PlayerPosition Position => _inner.Position;

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        await Task.Delay(_delay);
        return await _inner.ChooseCutAsync(deckSize, matchState);
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
        => _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        await Task.Delay(_delay);
        return await _inner.ChooseCardAsync(hand, handState, matchState, validPlays);
    }

    // Lifecycle/observation methods — no delay needed
    public Task OnDealStartedAsync(MatchState matchState) => _inner.OnDealStartedAsync(matchState);
    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState) => _inner.OnCardPlayedAsync(player, card, handState, matchState);
    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState) => _inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState);
    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState) => _inner.OnDealEndedAsync(result, handState, matchState);
    public Task OnMatchEndedAsync(MatchState matchState) => _inner.OnMatchEndedAsync(matchState);
    public Task ConfirmContinueDealAsync(MatchState matchState) => _inner.ConfirmContinueDealAsync(matchState);
    public Task ConfirmContinueMatchAsync(MatchState matchState) => _inner.ConfirmContinueMatchAsync(matchState);
}
