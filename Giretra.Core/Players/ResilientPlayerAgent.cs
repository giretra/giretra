using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Giretra.Core.Players;

/// <summary>
/// Decorator that catches exceptions from an inner player agent and returns safe defaults,
/// preventing a single failing agent (e.g. a remote bot server going down) from crashing the match.
/// After a configurable number of consecutive decision failures, switches to permanent fallback mode.
/// </summary>
public sealed class ResilientPlayerAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;
    private readonly ILogger _logger;
    private readonly int _maxConsecutiveFailures;

    private int _consecutiveFailures;
    private bool _permanentFallback;

    public ResilientPlayerAgent(
        IPlayerAgent inner,
        ILogger? logger = null,
        int maxConsecutiveFailures = 3)
    {
        _inner = inner;
        _logger = logger ?? NullLogger.Instance;
        _maxConsecutiveFailures = maxConsecutiveFailures;
    }

    public PlayerPosition Position => _inner.Position;

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        if (_permanentFallback)
            return (16, true);

        try
        {
            var result = await _inner.ChooseCutAsync(deckSize, matchState);
            ResetFailures();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex, nameof(ChooseCutAsync));
            return (16, true);
        }
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        if (_permanentFallback)
            return GetSafeNegotiationAction(validActions);

        try
        {
            var result = await _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);
            ResetFailures();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex, nameof(ChooseNegotiationActionAsync));
            return GetSafeNegotiationAction(validActions);
        }
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        if (_permanentFallback)
            return validPlays[0];

        try
        {
            var result = await _inner.ChooseCardAsync(hand, handState, matchState, validPlays);
            ResetFailures();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex, nameof(ChooseCardAsync));
            return validPlays[0];
        }
    }

    public async Task OnDealStartedAsync(MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.OnDealStartedAsync(matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(OnDealStartedAsync)); }
    }

    public async Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.OnCardPlayedAsync(player, card, handState, matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(OnCardPlayedAsync)); }
    }

    public async Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(OnTrickCompletedAsync)); }
    }

    public async Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.OnDealEndedAsync(result, handState, matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(OnDealEndedAsync)); }
    }

    public async Task OnMatchEndedAsync(MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.OnMatchEndedAsync(matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(OnMatchEndedAsync)); }
    }

    public async Task ConfirmContinueDealAsync(MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.ConfirmContinueDealAsync(matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(ConfirmContinueDealAsync)); }
    }

    public async Task ConfirmContinueMatchAsync(MatchState matchState)
    {
        if (_permanentFallback) return;
        try { await _inner.ConfirmContinueMatchAsync(matchState); }
        catch (Exception ex) { LogObservationError(ex, nameof(ConfirmContinueMatchAsync)); }
    }

    private static NegotiationAction GetSafeNegotiationAction(IReadOnlyList<NegotiationAction> validActions)
    {
        // Prefer Accept if available, otherwise take the first valid action
        foreach (var action in validActions)
        {
            if (action is AcceptAction)
                return action;
        }

        return validActions[0];
    }

    private void ResetFailures()
    {
        _consecutiveFailures = 0;
    }

    private void RecordFailure(Exception ex, string method)
    {
        _consecutiveFailures++;
        _logger.LogWarning(ex,
            "ResilientPlayerAgent [{Position}]: {Method} failed ({ConsecutiveFailures}/{Max}), using fallback",
            Position, method, _consecutiveFailures, _maxConsecutiveFailures);

        if (_consecutiveFailures >= _maxConsecutiveFailures)
        {
            _permanentFallback = true;
            _logger.LogWarning(
                "ResilientPlayerAgent [{Position}]: Switching to permanent fallback after {Count} consecutive failures",
                Position, _consecutiveFailures);
        }
    }

    private void LogObservationError(Exception ex, string method)
    {
        _logger.LogWarning(ex,
            "ResilientPlayerAgent [{Position}]: {Method} observation failed, continuing",
            Position, method);
    }
}
