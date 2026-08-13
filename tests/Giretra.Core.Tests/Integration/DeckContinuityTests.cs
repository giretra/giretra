using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;

namespace Giretra.Core.Tests.Integration;

/// <summary>
/// Verifies the deck is never reshuffled during a match: the deck provider
/// supplies only the first deal, subsequent deals reuse the cards collected
/// from the previous hand's tricks.
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
}
