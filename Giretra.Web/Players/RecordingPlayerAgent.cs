using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Web.Players;

public sealed class RecordingPlayerAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;
    private readonly ActionRecorder _recorder;

    public RecordingPlayerAgent(IPlayerAgent inner, ActionRecorder recorder)
    {
        _inner = inner;
        _recorder = recorder;
    }

    public PlayerPosition Position => _inner.Position;

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        var result = await _inner.ChooseCutAsync(deckSize, matchState);
        _recorder.RecordCut(Position, result.position, result.fromTop);
        return result;
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var result = await _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);

        var (actionType, gameMode) = result switch
        {
            AcceptAction => (RecordedActionType.Accept, (Core.GameModes.GameMode?)null),
            AnnouncementAction a => (RecordedActionType.Announce, (Core.GameModes.GameMode?)a.Mode),
            DoubleAction d => (RecordedActionType.Double, (Core.GameModes.GameMode?)d.TargetMode),
            RedoubleAction r => (RecordedActionType.Redouble, (Core.GameModes.GameMode?)r.TargetMode),
            _ => throw new ArgumentException($"Unknown action type: {result.GetType().Name}")
        };

        _recorder.RecordNegotiation(Position, actionType, gameMode);
        return result;
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var result = await _inner.ChooseCardAsync(hand, handState, matchState, validPlays);
        var trickNumber = handState.CompletedTricks.Count + 1;
        _recorder.RecordCardPlay(Position, result, trickNumber);
        return result;
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        _recorder.StartDeal(matchState.CompletedDeals.Count + 1, matchState.CurrentDealer);
        return _inner.OnDealStartedAsync(matchState);
    }

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
        => _inner.OnDealEndedAsync(result, handState, matchState);

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
