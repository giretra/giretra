using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// A player that makes random valid choices at each decision point.
/// Useful for testing and as a baseline opponent.
/// </summary>
public sealed class RandomPlayerAgent : IPlayerAgent
{
    private readonly Random _random;

    public PlayerPosition Position { get; }

    public RandomPlayerAgent(PlayerPosition position, int? seed = null)
        : this(position, seed.HasValue ? new Random(seed.Value) : new Random())
    {
    }

    public RandomPlayerAgent(PlayerPosition position, Random random)
    {
        Position = position;
        _random = random;
    }

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        // Cut must be between 6 and 26 cards
        int position = _random.Next(6, 27);
        bool fromTop = _random.Next(2) == 0;
        return Task.FromResult((position, fromTop));
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var chosenAction = validActions[_random.Next(validActions.Count)];
        return Task.FromResult(chosenAction);
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var chosenCard = validPlays[_random.Next(validPlays.Count)];
        return Task.FromResult(chosenCard);
    }

    public Task OnDealStartedAsync(MatchState matchState) => Task.CompletedTask;

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState) => Task.CompletedTask;

    public Task OnMatchEndedAsync(MatchState matchState) => Task.CompletedTask;

    public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState) => Task.CompletedTask;

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState) => Task.CompletedTask;
}
