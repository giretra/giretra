using System.Diagnostics;
using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Manage.Validation;

/// <summary>
/// Wraps an <see cref="IPlayerAgent"/> to time and validate every decision.
/// Records violations but does not throw, allowing matches to continue.
/// </summary>
public sealed class ValidatingPlayerAgent : IPlayerAgent
{
    private readonly IPlayerAgent _inner;
    private readonly int? _timeoutMs;
    private int _matchNumber;
    private int _dealNumber;

    private readonly List<Violation> _violations = [];
    private readonly List<DecisionTiming> _timings = [];
    private readonly List<NotificationWarning> _notificationWarnings = [];
    private readonly List<(DecisionType Type, string Value)> _decisions = [];
    private readonly bool _recordDecisions;

    public PlayerPosition Position => _inner.Position;

    public IReadOnlyList<Violation> Violations => _violations;
    public IReadOnlyList<DecisionTiming> Timings => _timings;
    public IReadOnlyList<NotificationWarning> NotificationWarnings => _notificationWarnings;
    public IReadOnlyList<(DecisionType Type, string Value)> Decisions => _decisions;

    public ValidatingPlayerAgent(IPlayerAgent inner, int? timeoutMs, bool recordDecisions = false)
    {
        _inner = inner;
        _timeoutMs = timeoutMs;
        _recordDecisions = recordDecisions;
    }

    public void SetMatchNumber(int matchNumber)
    {
        _matchNumber = matchNumber;
        _dealNumber = 0;
    }

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        var sw = Stopwatch.StartNew();
        var result = await _inner.ChooseCutAsync(deckSize, matchState);
        sw.Stop();

        _timings.Add(new DecisionTiming(DecisionType.Cut, sw.Elapsed, _matchNumber));
        CheckTimeout(DecisionType.Cut, sw.Elapsed);

        if (result.position < 6 || result.position > 26)
        {
            _violations.Add(new Violation(
                DecisionType.Cut,
                $"Cut position {result.position} is out of range [6, 26]",
                _matchNumber, _dealNumber));
        }

        if (_recordDecisions)
            _decisions.Add((DecisionType.Cut, $"({result.position}, {result.fromTop})"));

        return result;
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var sw = Stopwatch.StartNew();
        var result = await _inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);
        sw.Stop();

        _timings.Add(new DecisionTiming(DecisionType.Negotiation, sw.Elapsed, _matchNumber));
        CheckTimeout(DecisionType.Negotiation, sw.Elapsed);

        if (!validActions.Contains(result))
        {
            _violations.Add(new Violation(
                DecisionType.Negotiation,
                $"Returned action '{result}' is not in the valid actions list: [{string.Join(", ", validActions)}]",
                _matchNumber, _dealNumber));
        }

        if (_recordDecisions)
            _decisions.Add((DecisionType.Negotiation, result.ToString()!));

        return result;
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var sw = Stopwatch.StartNew();
        var result = await _inner.ChooseCardAsync(hand, handState, matchState, validPlays);
        sw.Stop();

        _timings.Add(new DecisionTiming(DecisionType.CardPlay, sw.Elapsed, _matchNumber));
        CheckTimeout(DecisionType.CardPlay, sw.Elapsed);

        if (!validPlays.Contains(result))
        {
            _violations.Add(new Violation(
                DecisionType.CardPlay,
                $"Played card '{result}' is not in valid plays: [{string.Join(", ", validPlays)}]",
                _matchNumber, _dealNumber));
        }

        if (_recordDecisions)
            _decisions.Add((DecisionType.CardPlay, result.ToString()));

        return result;
    }

    public async Task OnDealStartedAsync(MatchState matchState)
    {
        _dealNumber++;
        await TimeNotificationAsync(nameof(OnDealStartedAsync), () => _inner.OnDealStartedAsync(matchState));
    }

    public async Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        await TimeNotificationAsync(nameof(OnDealEndedAsync),
            () => _inner.OnDealEndedAsync(result, handState, matchState));
    }

    public async Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        await TimeNotificationAsync(nameof(OnCardPlayedAsync),
            () => _inner.OnCardPlayedAsync(player, card, handState, matchState));
    }

    public async Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        await TimeNotificationAsync(nameof(OnTrickCompletedAsync),
            () => _inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState));
    }

    public async Task OnMatchEndedAsync(MatchState matchState)
    {
        await TimeNotificationAsync(nameof(OnMatchEndedAsync),
            () => _inner.OnMatchEndedAsync(matchState));
    }

    public Task ConfirmContinueDealAsync(MatchState matchState)
        => _inner.ConfirmContinueDealAsync(matchState);

    public Task ConfirmContinueMatchAsync(MatchState matchState)
        => _inner.ConfirmContinueMatchAsync(matchState);

    private async Task TimeNotificationAsync(string methodName, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _notificationWarnings.Add(new NotificationWarning(
                methodName, ex.Message, _matchNumber, _dealNumber));
        }
        finally
        {
            sw.Stop();
            _timings.Add(new DecisionTiming(DecisionType.Notification, sw.Elapsed, _matchNumber));
        }
    }

    private void CheckTimeout(DecisionType type, TimeSpan elapsed)
    {
        if (_timeoutMs.HasValue && elapsed.TotalMilliseconds > _timeoutMs.Value)
        {
            _violations.Add(new Violation(
                type,
                $"Response time {elapsed.TotalMilliseconds:F1}ms exceeded timeout of {_timeoutMs.Value}ms",
                _matchNumber, _dealNumber));
        }
    }
}
