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
    private readonly GameSession _session;
    private readonly INotificationService _notifications;
    private readonly string _clientId;
    private readonly TimeSpan _timeout;

    public PlayerPosition Position { get; }

    public WebApiPlayerAgent(
        PlayerPosition position,
        string clientId,
        GameSession session,
        INotificationService notifications,
        TimeSpan? timeout = null)
    {
        Position = position;
        _clientId = clientId;
        _session = session;
        _notifications = notifications;
        _timeout = timeout ?? TimeSpan.FromMinutes(2);
    }

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        var tcs = new TaskCompletionSource<(int position, bool fromTop)>();

        _session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.Cut,
            Player = Position,
            CutTcs = tcs
        };

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.Cut);

        // Wait for the action with timeout
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - use default cut (middle of deck)
            tcs.TrySetResult((16, true));
            return (16, true);
        }
        finally
        {
            _session.PendingAction = null;
        }
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var tcs = new TaskCompletionSource<NegotiationAction>();

        _session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.Negotiate,
            Player = Position,
            NegotiationTcs = tcs,
            ValidNegotiationActions = validActions
        };

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.Negotiate);

        // Wait for the action with timeout
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - pick first valid action (usually Accept)
            var defaultAction = validActions.FirstOrDefault(a => a is AcceptAction) ?? validActions[0];
            tcs.TrySetResult(defaultAction);
            return defaultAction;
        }
        finally
        {
            _session.PendingAction = null;
        }
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var tcs = new TaskCompletionSource<Card>();

        _session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.PlayCard,
            Player = Position,
            PlayCardTcs = tcs,
            ValidCards = validPlays
        };

        // Notify the player it's their turn
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.PlayCard);

        // Wait for the action with timeout
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - play first valid card
            var defaultCard = validPlays[0];
            tcs.TrySetResult(defaultCard);
            return defaultCard;
        }
        finally
        {
            _session.PendingAction = null;
        }
    }

    public async Task OnDealStartedAsync(MatchState matchState)
    {
        await _notifications.NotifyDealStartedAsync(_session.GameId, matchState);
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

        _session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.ContinueDeal,
            Player = Position,
            ContinueDealTcs = tcs
        };

        // Notify the player to confirm continuation
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.ContinueDeal);

        // Wait for the confirmation with timeout
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - auto-continue
            tcs.TrySetResult(true);
        }
        finally
        {
            _session.PendingAction = null;
        }
    }

    public async Task ConfirmContinueMatchAsync(MatchState matchState)
    {
        var tcs = new TaskCompletionSource<bool>();

        _session.PendingAction = new PendingAction
        {
            ActionType = PendingActionType.ContinueMatch,
            Player = Position,
            ContinueMatchTcs = tcs
        };

        // Notify the player to confirm continuation
        await _notifications.NotifyYourTurnAsync(_session.GameId, _clientId, Position, PendingActionType.ContinueMatch);

        // Wait for the confirmation with timeout
        using var cts = new CancellationTokenSource(_timeout);
        try
        {
            await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - auto-continue
            tcs.TrySetResult(true);
        }
        finally
        {
            _session.PendingAction = null;
        }
    }
}
