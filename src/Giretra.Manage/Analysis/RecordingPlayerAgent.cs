using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Manage.Analysis;

/// <summary>
/// Wraps an <see cref="IPlayerAgent"/> and reports every completed deal (result + full play record).
/// </summary>
public sealed class RecordingPlayerAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;
    private readonly Action<DealResult, HandState> _onDealEnded;

    public RecordingPlayerAgent(IPlayerAgent inner, Action<DealResult, HandState> onDealEnded)
    {
        _inner = inner;
        _onDealEnded = onDealEnded;
    }

    public PlayerPosition Position => _inner.Position;

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
        => _inner.ChooseCutAsync(deckSize, matchState);

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
        => _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
        => _inner.ChooseCardAsync(hand, handState, matchState, validPlays);

    public Task OnDealStartedAsync(MatchState matchState)
        => _inner.OnDealStartedAsync(matchState);

    public Task OnNegotiationCompletedAsync(NegotiationState negotiationState, MatchState matchState)
        => _inner.OnNegotiationCompletedAsync(negotiationState, matchState);

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        _onDealEnded(result, handState);
        return _inner.OnDealEndedAsync(result, handState, matchState);
    }

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
        => _inner.OnCardPlayedAsync(player, card, handState, matchState);

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
        => _inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState);

    public Task OnMatchEndedAsync(MatchState matchState)
        => _inner.OnMatchEndedAsync(matchState);

    public Task ConfirmContinueDealAsync(MatchState matchState)
        => _inner.ConfirmContinueDealAsync(matchState);

    public Task ConfirmContinueMatchAsync(MatchState matchState)
        => _inner.ConfirmContinueMatchAsync(matchState);
}
