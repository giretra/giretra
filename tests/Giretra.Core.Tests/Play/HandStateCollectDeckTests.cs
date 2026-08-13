using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Play;

/// <summary>
/// Tests for rebuilding the deck from a completed hand's tricks.
/// The requested team's tricks go on top of the other team's tricks;
/// within each pile, tricks are in the order they were won, cards in play order.
/// </summary>
public class HandStateCollectDeckTests
{
    private static Card C(CardRank rank, CardSuit suit) => new(rank, suit);

    /// <summary>
    /// Plays a scripted hand of 8 single-suit tricks (Colour Clubs, first leader Left).
    /// Winners: Top, Right, Right, Bottom, Bottom, Left, Left, Top
    /// → Team1 wins tricks 1, 4, 5, 8; Team2 wins tricks 2, 3, 6, 7.
    /// </summary>
    private static HandState PlayScriptedHand()
    {
        var hand = HandState.Create(GameMode.ColourClubs, PlayerPosition.Left);

        var tricks = new[]
        {
            // Trick 1 - leader Left: Kh, Ah, Qh, Jh → Top wins (Ace of hearts)
            new[] { C(CardRank.King, CardSuit.Hearts), C(CardRank.Ace, CardSuit.Hearts), C(CardRank.Queen, CardSuit.Hearts), C(CardRank.Jack, CardSuit.Hearts) },
            // Trick 2 - leader Top: 7h, 10h, 8h, 9h → Right wins (Ten of hearts)
            new[] { C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Eight, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Hearts) },
            // Trick 3 - leader Right: As, Ks, Qs, Js → Right wins (Ace of spades)
            new[] { C(CardRank.Ace, CardSuit.Spades), C(CardRank.King, CardSuit.Spades), C(CardRank.Queen, CardSuit.Spades), C(CardRank.Jack, CardSuit.Spades) },
            // Trick 4 - leader Right: 7s, 10s, 8s, 9s → Bottom wins (Ten of spades)
            new[] { C(CardRank.Seven, CardSuit.Spades), C(CardRank.Ten, CardSuit.Spades), C(CardRank.Eight, CardSuit.Spades), C(CardRank.Nine, CardSuit.Spades) },
            // Trick 5 - leader Bottom: Ad, Kd, Qd, Jd → Bottom wins (Ace of diamonds)
            new[] { C(CardRank.Ace, CardSuit.Diamonds), C(CardRank.King, CardSuit.Diamonds), C(CardRank.Queen, CardSuit.Diamonds), C(CardRank.Jack, CardSuit.Diamonds) },
            // Trick 6 - leader Bottom: 9d, 10d, 7d, 8d → Left wins (Ten of diamonds)
            new[] { C(CardRank.Nine, CardSuit.Diamonds), C(CardRank.Ten, CardSuit.Diamonds), C(CardRank.Seven, CardSuit.Diamonds), C(CardRank.Eight, CardSuit.Diamonds) },
            // Trick 7 - leader Left: Jc, 9c, Ac, 10c → Left wins (Jack of trumps)
            new[] { C(CardRank.Jack, CardSuit.Clubs), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Ten, CardSuit.Clubs) },
            // Trick 8 - leader Left: 7c, Kc, Qc, 8c → Top wins (King of trumps)
            new[] { C(CardRank.Seven, CardSuit.Clubs), C(CardRank.King, CardSuit.Clubs), C(CardRank.Queen, CardSuit.Clubs), C(CardRank.Eight, CardSuit.Clubs) }
        };

        foreach (var trick in tricks)
        {
            foreach (var card in trick)
            {
                hand = hand.PlayCard(card);
            }
        }

        return hand;
    }

    [Fact]
    public void TrickWinners_TracksWinnerOfEachTrick()
    {
        var hand = PlayScriptedHand();

        Assert.True(hand.IsComplete);
        Assert.Equal(
            new[]
            {
                PlayerPosition.Top, PlayerPosition.Right, PlayerPosition.Right, PlayerPosition.Bottom,
                PlayerPosition.Bottom, PlayerPosition.Left, PlayerPosition.Left, PlayerPosition.Top
            },
            hand.TrickWinners);
    }

    [Fact]
    public void CollectDeck_PutsTopTeamTricksFirst_InWonOrder_CardsInPlayOrder()
    {
        var hand = PlayScriptedHand();

        var deck = hand.CollectDeck(Team.Team1);

        var expected = new[]
        {
            // Team1 tricks: 1, 4, 5, 8
            C(CardRank.King, CardSuit.Hearts), C(CardRank.Ace, CardSuit.Hearts), C(CardRank.Queen, CardSuit.Hearts), C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Seven, CardSuit.Spades), C(CardRank.Ten, CardSuit.Spades), C(CardRank.Eight, CardSuit.Spades), C(CardRank.Nine, CardSuit.Spades),
            C(CardRank.Ace, CardSuit.Diamonds), C(CardRank.King, CardSuit.Diamonds), C(CardRank.Queen, CardSuit.Diamonds), C(CardRank.Jack, CardSuit.Diamonds),
            C(CardRank.Seven, CardSuit.Clubs), C(CardRank.King, CardSuit.Clubs), C(CardRank.Queen, CardSuit.Clubs), C(CardRank.Eight, CardSuit.Clubs),
            // Team2 tricks: 2, 3, 6, 7
            C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Eight, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Hearts),
            C(CardRank.Ace, CardSuit.Spades), C(CardRank.King, CardSuit.Spades), C(CardRank.Queen, CardSuit.Spades), C(CardRank.Jack, CardSuit.Spades),
            C(CardRank.Nine, CardSuit.Diamonds), C(CardRank.Ten, CardSuit.Diamonds), C(CardRank.Seven, CardSuit.Diamonds), C(CardRank.Eight, CardSuit.Diamonds),
            C(CardRank.Jack, CardSuit.Clubs), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Ten, CardSuit.Clubs)
        };

        Assert.Equal(expected, deck.Cards);
    }

    [Fact]
    public void CollectDeck_ForOtherTeam_PutsTheirTricksOnTop()
    {
        var hand = PlayScriptedHand();

        var deck = hand.CollectDeck(Team.Team2);

        // Team2's first won trick (trick 2) is on top
        Assert.Equal(C(CardRank.Seven, CardSuit.Hearts), deck[0]);
        Assert.Equal(32, deck.Count);
        Assert.Equal(32, deck.Cards.Distinct().Count());
    }

    [Fact]
    public void CollectDeck_ContainsAll32Cards()
    {
        var hand = PlayScriptedHand();

        var deck = hand.CollectDeck(Team.Team1);

        Assert.Equal(32, deck.Count);
        Assert.Equal(
            Deck.CreateStandard().Cards.OrderBy(c => (c.Suit, c.Rank)),
            deck.Cards.OrderBy(c => (c.Suit, c.Rank)));
    }

    [Fact]
    public void CollectDeck_Throws_WhenHandIncomplete()
    {
        var hand = HandState.Create(GameMode.ColourClubs, PlayerPosition.Left);

        Assert.Throws<InvalidOperationException>(() => hand.CollectDeck(Team.Team1));
    }
}
