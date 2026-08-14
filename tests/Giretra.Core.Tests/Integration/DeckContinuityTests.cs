using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Integration;

/// <summary>
/// Verifies the deck is never reshuffled during a match: the deck provider
/// supplies only the first deal, subsequent deals reuse the cards collected
/// from the previous hand's tricks. Across matches, <see cref="GameManager.FinalDeck"/>
/// hands the collected deck to the next match so the same cards keep circulating.
/// </summary>
public class DeckContinuityTests
{
    [Fact]
    public async Task Match_UsesDeckProviderOnlyForTheFirstDeal()
    {
        var providerCalls = 0;
        var deckRandom = new Random(42);
        Func<Deck> deckProvider = () =>
        {
            providerCalls++;
            return Deck.CreateShuffled(deckRandom);
        };

        var gameManager = new GameManager(
            new RandomPlayerAgent(PlayerPosition.Bottom, seed: 1),
            new RandomPlayerAgent(PlayerPosition.Left, seed: 2),
            new RandomPlayerAgent(PlayerPosition.Top, seed: 3),
            new RandomPlayerAgent(PlayerPosition.Right, seed: 4),
            PlayerPosition.Bottom,
            deckProvider);

        var matchState = await gameManager.PlayMatchAsync();

        Assert.True(matchState.IsComplete);
        Assert.True(matchState.CompletedDeals.Count > 1,
            "Match should span multiple deals for this test to be meaningful.");
        Assert.Equal(1, providerCalls);
    }

    /// <summary>
    /// After a match, <see cref="GameManager.FinalDeck"/> is the deck collected from the last
    /// hand's tricks, and a next match created with it as deck provider deals its first deal
    /// from exactly those cards — the deck is never shuffled between games either.
    /// </summary>
    [Fact]
    public async Task NextMatch_IsDealtFromTheDeckTheLastMatchLeftOnTheTable()
    {
        var tracker = new DeckTracker();
        var firstManager = new GameManager(
            new ObservingAgent(new RandomPlayerAgent(PlayerPosition.Bottom, seed: 1), tracker),
            new RandomPlayerAgent(PlayerPosition.Left, seed: 2),
            new RandomPlayerAgent(PlayerPosition.Top, seed: 3),
            new RandomPlayerAgent(PlayerPosition.Right, seed: 4),
            PlayerPosition.Bottom,
            () => Deck.CreateShuffled(new Random(7)));

        await firstManager.PlayMatchAsync();

        var tableDeck = firstManager.FinalDeck;
        Assert.NotNull(tableDeck);
        Assert.Equal(32, tableDeck.Count);
        Assert.Equal(tracker.PredictedDeck!.Cards, tableDeck.Cards);

        var observer = new ObservingAgent(new RandomPlayerAgent(PlayerPosition.Bottom, seed: 5), new DeckTracker());
        var secondManager = new GameManager(
            observer,
            new RandomPlayerAgent(PlayerPosition.Left, seed: 6),
            new RandomPlayerAgent(PlayerPosition.Top, seed: 7),
            new RandomPlayerAgent(PlayerPosition.Right, seed: 8),
            PlayerPosition.Left,
            () => tableDeck);

        await secondManager.PlayMatchAsync();

        Assert.Equal(tableDeck.Cards, observer.FirstDealDeck!.Cards);
    }

    /// <summary>
    /// Delegates all decisions and observes the game the way a player at the table can:
    /// feeds a <see cref="DeckTracker"/> and records the pre-cut deck of the first deal.
    /// </summary>
    private sealed class ObservingAgent(IPlayerAgent inner, DeckTracker tracker) : IPlayerAgent
    {
        public Deck? FirstDealDeck { get; private set; }

        public PlayerPosition Position => inner.Position;

        public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
            => inner.ChooseCutAsync(deckSize, matchState);

        public Task<NegotiationAction> ChooseNegotiationActionAsync(
            IReadOnlyList<Card> hand,
            NegotiationState negotiationState,
            MatchState matchState,
            IReadOnlyList<NegotiationAction> validActions)
            => inner.ChooseNegotiationActionAsync(hand, negotiationState, matchState, validActions);

        public Task<Card> ChooseCardAsync(
            IReadOnlyList<Card> hand,
            HandState handState,
            MatchState matchState,
            IReadOnlyList<Card> validPlays)
            => inner.ChooseCardAsync(hand, handState, matchState, validPlays);

        public Task OnDealStartedAsync(MatchState matchState)
        {
            FirstDealDeck ??= matchState.CurrentDeal!.Deck;
            tracker.OnDealStarted(matchState);
            return inner.OnDealStartedAsync(matchState);
        }

        public Task OnNegotiationCompletedAsync(NegotiationState negotiationState, MatchState matchState)
            => inner.OnNegotiationCompletedAsync(negotiationState, matchState);

        public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
        {
            tracker.OnDealEnded(handState);
            return inner.OnDealEndedAsync(result, handState, matchState);
        }

        public Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
            => inner.OnCardPlayedAsync(player, card, handState, matchState);

        public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
            => inner.OnTrickCompletedAsync(completedTrick, winner, handState, matchState);

        public Task OnMatchEndedAsync(MatchState matchState) => inner.OnMatchEndedAsync(matchState);

        public Task ConfirmContinueDealAsync(MatchState matchState) => inner.ConfirmContinueDealAsync(matchState);

        public Task ConfirmContinueMatchAsync(MatchState matchState) => inner.ConfirmContinueMatchAsync(matchState);
    }
}
