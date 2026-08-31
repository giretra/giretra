using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Services;

namespace Giretra.Web.Players;

/// <summary>
/// Player agent that bridges the GameManager's async flow with HTTP requests.
/// Uses TaskCompletionSource to wait for player actions submitted via API.
/// </summary>
public sealed class WebApiPlayerAgent : IPlayerAgent
{
    /// <summary>
    /// How long players get to click "Play Again" after a match ends.
    /// Deliberately independent of the room's turn timer, which is far too
    /// short for players reading the match recap. Kept in sync with the
    /// room idle timeout so a stuck table still closes in reasonable time.
    /// </summary>
    public static readonly TimeSpan DefaultContinueMatchTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How often to re-check the connection state while the game is paused
    /// waiting for a disconnected player to come back.
    /// </summary>
    private static readonly TimeSpan DisconnectPollInterval = TimeSpan.FromSeconds(1);

    private readonly GameSession _session;
    private readonly INotificationService _notifications;
    private string _clientId;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _continueMatchTimeout;
    private readonly Func<string, bool> _shouldPauseOnTimeout;

    public PlayerPosition Position { get; }
    public string ClientId => _clientId;

    /// <summary>
    /// Updates the client ID when a player rejoins with a new session.
    /// </summary>
    public void UpdateClientId(string newClientId)
    {
        _clientId = newClientId;
    }

    public WebApiPlayerAgent(
        PlayerPosition position,
        string clientId,
        GameSession session,
        INotificationService notifications,
        TimeSpan? timeout = null,
        TimeSpan? continueMatchTimeout = null,
        Func<string, bool>? shouldPauseOnTimeout = null)
    {
        Position = position;
        _clientId = clientId;
        _session = session;
        _notifications = notifications;
        _timeout = timeout ?? TimeSpan.FromMinutes(2);
        _continueMatchTimeout = continueMatchTimeout ?? DefaultContinueMatchTimeout;
        _shouldPauseOnTimeout = shouldPauseOnTimeout ?? (_ => false);
    }

    /// <summary>
    /// Timeout source linked to the session's cancellation token, so an
    /// abandoned/terminated game interrupts the wait instead of running it out.
    /// </summary>
    private CancellationTokenSource CreateTimeoutSource(TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_session.CancellationTokenSource.Token);
        cts.CancelAfter(timeout);
        return cts;
    }

    /// <summary>
    /// Rethrows when the game itself was cancelled (abandon/terminate), so the
    /// game loop stops instead of playing out the deal with timeout defaults.
    /// </summary>
    private void ThrowIfGameCancelled()
    {
        _session.CancellationTokenSource.Token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Waits for the pending action to be resolved. On timeout, a connected
    /// player gets the default move; a disconnected one pauses the game until
    /// they reconnect (fresh timer, re-notified) instead of having their hand
    /// played out with defaults. The room's abandoned-table cleanup remains the
    /// backstop that eventually cancels a game nobody comes back to.
    /// </summary>
    private async Task<T> WaitForActionAsync<T>(PendingAction pending, TaskCompletionSource<T> tcs, Func<T> timeoutDefault)
    {
        while (true)
        {
            using var cts = CreateTimeoutSource(pending.TimeoutDuration);
            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                ThrowIfGameCancelled();

                if (!_shouldPauseOnTimeout(_clientId))
                {
                    var result = timeoutDefault();
                    tcs.TrySetResult(result);
                    return result;
                }

                while (_shouldPauseOnTimeout(_clientId) && !tcs.Task.IsCompleted)
                    await Task.Delay(DisconnectPollInterval, _session.CancellationTokenSource.Token);

                if (!tcs.Task.IsCompleted)
                {
                    pending.RestartTimeout();
                    await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, pending.ActionType, pending.TimeoutAt);
                }
            }
        }
    }

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        var tcs = new TaskCompletionSource<(int position, bool fromTop)>();

        var pending = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = Position,
            CutTcs = tcs,
            TimeoutDuration = _timeout
        };
        _session.PendingActions[Position] = pending;

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.Cut, pending.TimeoutAt);

        try
        {
            // Timeout default: cut in the middle of the deck
            return await WaitForActionAsync(pending, tcs, () => (16, true));
        }
        finally
        {
            _session.PendingActions.TryRemove(Position, out _);
        }
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var tcs = new TaskCompletionSource<NegotiationAction>();

        var pending = new PendingAction
        {
            ActionType = PendingActionType.Negotiate,
            Player = Position,
            NegotiationTcs = tcs,
            ValidNegotiationActions = validActions,
            TimeoutDuration = _timeout
        };
        _session.PendingActions[Position] = pending;

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.Negotiate, pending.TimeoutAt);

        try
        {
            // Timeout default: first valid action (usually Accept)
            return await WaitForActionAsync(pending, tcs,
                () => validActions.FirstOrDefault(a => a is AcceptAction) ?? validActions[0]);
        }
        finally
        {
            _session.PendingActions.TryRemove(Position, out _);
        }
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var tcs = new TaskCompletionSource<Card>();

        var pending = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = Position,
            PlayCardTcs = tcs,
            ValidCards = validPlays,
            TimeoutDuration = _timeout
        };
        _session.PendingActions[Position] = pending;

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.PlayCard, pending.TimeoutAt);

        try
        {
            // Timeout default: first valid card
            return await WaitForActionAsync(pending, tcs, () => validPlays[0]);
        }
        finally
        {
            _session.PendingActions.TryRemove(Position, out _);
        }
    }

    public async Task OnDealStartedAsync(MatchState matchState)
    {
        await _notifications.NotifyDealStartedAsync(_session.GameId, matchState);
    }

    public async Task OnNegotiationCompletedAsync(NegotiationState negotiationState, MatchState matchState)
    {
        await _notifications.NotifyNegotiationCompletedAsync(_session.GameId, negotiationState, matchState);
    }

    public async Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        await _notifications.NotifyDealEndedAsync(_session.GameId, result, handState, matchState);
    }

    public async Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        await _notifications.NotifyCardPlayedAsync(_session.GameId, player, card, handState, matchState);
    }

    public async Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        await _notifications.NotifyTrickCompletedAsync(_session.GameId, completedTrick, winner, handState, matchState);
    }

    public async Task OnMatchEndedAsync(MatchState matchState)
    {
        await _notifications.NotifyMatchEndedAsync(_session.GameId, matchState);
    }

    public async Task ConfirmContinueDealAsync(MatchState matchState)
    {
        var tcs = new TaskCompletionSource<bool>();

        var pending = new PendingAction
        {
            ActionType = PendingActionType.ContinueDeal,
            Player = Position,
            ContinueDealTcs = tcs,
            TimeoutDuration = _timeout
        };
        _session.PendingActions[Position] = pending;

        // Notify the player to confirm continuation
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.ContinueDeal, pending.TimeoutAt);

        try
        {
            // Timeout default: auto-continue
            await WaitForActionAsync(pending, tcs, () => true);
        }
        finally
        {
            _session.PendingActions.TryRemove(Position, out _);
        }
    }

    public async Task ConfirmContinueMatchAsync(MatchState matchState)
    {
        var tcs = new TaskCompletionSource<bool>();

        var pending = new PendingAction
        {
            ActionType = PendingActionType.ContinueMatch,
            Player = Position,
            ContinueMatchTcs = tcs,
            TimeoutDuration = _continueMatchTimeout
        };
        _session.PendingActions[Position] = pending;

        // Notify the player to confirm continuation
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.ContinueMatch, pending.TimeoutAt);

        // Wait for the confirmation with timeout
        using var cts = CreateTimeoutSource(_continueMatchTimeout);
        try
        {
            await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            ThrowIfGameCancelled();

            // Timeout - treat as "no play again". Deliberately no disconnect
            // pause here: the match is over, holding the session open for a
            // disconnected player would only delay room cleanup.
            tcs.TrySetResult(false);
        }
        finally
        {
            _session.PendingActions.TryRemove(Position, out _);
        }
    }
}
