using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Players;

/// <summary>
/// Verifies the deck order a player can infer from the tricks it watched.
/// </summary>
public class DeckTrackerTests
{
    [Fact]
    public void ANewTracker_KnowsNothing()
    {
        var tracker = new DeckTracker();

        Assert.Null(tracker.PredictedDeck);
        Assert.False(tracker.IsReliable);
    }

    [Fact]
    public void AnIncompleteHand_LeavesTheOrderUnknown()
    {
        var tracker = new DeckTracker();
        tracker.OnDealStarted(NewDeal(PlayerPosition.Bottom));

        tracker.OnDealEnded(HandState.Create(GameMode.ColourHearts, PlayerPosition.Left));

        Assert.False(tracker.IsReliable);
    }

    [Fact]
    public void ACompletedHand_YieldsTheNextDealsDeck()
    {
        var tracker = new DeckTracker();
        tracker.OnDealStarted(NewDeal(PlayerPosition.Bottom));

        var hand = PlayOutAHand();
        tracker.OnDealEnded(hand);

        Assert.True(tracker.IsReliable);
        Assert.Equal(hand.CollectDeck(Team.Team1).Cards, tracker.PredictedDeck!.Cards);
    }

    /// <summary>
    /// The dealer decides which team's tricks go on top, and the match state has already rotated
    /// to the next dealer by the time a deal ends, so the tracker has to use the dealer it saw at
    /// the start of the deal.
    /// </summary>
    [Fact]
    public void TheDealerOfTheFinishedDeal_DecidesWhichTricksGoOnTop()
    {
        var tracker = new DeckTracker();

        // Bottom (Team1) deals this hand; the next dealer is Left (Team2).
        tracker.OnDealStarted(NewDeal(PlayerPosition.Bottom));
        var hand = PlayOutAHand();
        tracker.OnDealEnded(hand);

        var dealerTeamOnTop = hand.CollectDeck(Team.Team1).Cards;
        var nextDealerTeamOnTop = hand.CollectDeck(Team.Team2).Cards;

        Assert.NotEqual(nextDealerTeamOnTop, dealerTeamOnTop);
        Assert.Equal(dealerTeamOnTop, tracker.PredictedDeck!.Cards);
    }

    [Fact]
    public void Reset_ForgetsTheOrder()
    {
        var tracker = new DeckTracker();
        tracker.OnDealStarted(NewDeal(PlayerPosition.Bottom));
        tracker.OnDealEnded(PlayOutAHand());

        tracker.Reset();

        Assert.False(tracker.IsReliable);
    }

    [Fact]
    public void Invalidate_DropsTheOrder()
    {
        var tracker = new DeckTracker();
        tracker.OnDealStarted(NewDeal(PlayerPosition.Bottom));
        tracker.OnDealEnded(PlayOutAHand());

        tracker.Invalidate();

        Assert.False(tracker.IsReliable);
    }

    private static MatchState NewDeal(PlayerPosition dealer)
        => MatchState.Create(dealer).StartDeal(Deck.CreateShuffled(new Random(11)));

    /// <summary>
    /// Plays all 32 cards into a hand so it can be collected into a deck. Cards are played in a
    /// fixed order and the trick winners follow from the mode, so both teams win tricks.
    /// </summary>
    private static HandState PlayOutAHand()
    {
        var hand = HandState.Create(GameMode.ColourHearts, PlayerPosition.Left);

        foreach (var card in Deck.CreateShuffled(new Random(99)).Cards)
        {
            hand = hand.PlayCard(card);
        }

        return hand;
    }
}
