using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players.Agents.Remote;

/// <summary>
/// A player agent that delegates all decisions to a remote HTTP bot server.
/// Session creation is deferred to the first async call (lazy initialization).
/// </summary>
public sealed class RemotePlayerAgent : IPlayerAgent, IAsyncDisposable
{
    private readonly RemoteBotClient _client;
    private readonly string _matchId;
    private string? _sessionId;

    public PlayerPosition Position { get; }

    public RemotePlayerAgent(RemoteBotClient client, PlayerPosition position, string matchId)
    {
        _client = client;
        Position = position;
        _matchId = matchId;
    }

    private async Task EnsureSessionAsync()
    {
        _sessionId ??= await _client.CreateSessionAsync(Position, _matchId);
    }

    public async Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        await EnsureSessionAsync();
        return await _client.ChooseCutAsync(_sessionId!, deckSize, matchState);
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        await EnsureSessionAsync();
        return await _client.ChooseNegotiationActionAsync(
            _sessionId!, Position, hand, negotiationState, matchState, validActions);
    }

    public async Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        await EnsureSessionAsync();
        return await _client.ChooseCardAsync(_sessionId!, hand, handState, matchState, validPlays);
    }

    public async Task OnDealStartedAsync(MatchState matchState)
    {
        await EnsureSessionAsync();
        await _client.NotifyDealStartedAsync(_sessionId!, matchState);
    }

    public async Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        if (_sessionId is null) return;
        await _client.NotifyDealEndedAsync(_sessionId, result, handState, matchState);
    }

    public async Task OnCardPlayedAsync(
        PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        if (_sessionId is null) return;
        await _client.NotifyCardPlayedAsync(_sessionId, player, card, handState, matchState);
    }

    public async Task OnTrickCompletedAsync(
        TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        if (_sessionId is null) return;
        await _client.NotifyTrickCompletedAsync(_sessionId, completedTrick, winner, handState, matchState);
    }

    public async Task OnMatchEndedAsync(MatchState matchState)
    {
        if (_sessionId is null) return;
        await _client.NotifyMatchEndedAsync(_sessionId, matchState);
    }

    /// <summary>
    /// Auto-confirms — remote bots don't need human confirmation gates.
    /// </summary>
    public Task ConfirmContinueDealAsync(MatchState matchState) => Task.CompletedTask;

    /// <summary>
    /// Auto-confirms — remote bots don't need human confirmation gates.
    /// </summary>
    public Task ConfirmContinueMatchAsync(MatchState matchState) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_sessionId is not null)
        {
            await _client.DestroySessionAsync(_sessionId);
            _sessionId = null;
        }
    }
}
