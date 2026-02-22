using Giretra.Core.Cards;

namespace Giretra.Core.Tests.Cards;

public class DeckTests
{
    [Fact]
    public void CreateStandard_Returns32Cards()
    {
        var deck = Deck.CreateStandard();
        Assert.Equal(32, deck.Count);
    }

    [Fact]
    public void CreateStandard_ContainsAllRanksAndSuits()
    {
        var deck = Deck.CreateStandard();

        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var rank in Enum.GetValues<CardRank>())
            {
                Assert.True(deck.Contains(new Card(rank, suit)),
                    $"Missing {rank} of {suit}");
            }
        }
    }

    [Fact]
    public void CreateStandard_HasNoDuplicates()
    {
        var deck = Deck.CreateStandard();
        var uniqueCards = deck.Cards.Distinct().Count();
        Assert.Equal(32, uniqueCards);
    }

    [Theory]
    [InlineData(6, true)]   // Minimum from top
    [InlineData(26, true)]  // Maximum from top
    [InlineData(6, false)]  // Minimum from bottom
    [InlineData(26, false)] // Maximum from bottom
    [InlineData(16, true)]  // Middle from top
    public void Cut_ValidPositions_Succeeds(int position, bool fromTop)
    {
        var deck = Deck.CreateStandard();
        var cutDeck = deck.Cut(position, fromTop);

        Assert.Equal(32, cutDeck.Count);
    }

    [Theory]
    [InlineData(5)]  // Too few
    [InlineData(27)] // Too many
    [InlineData(0)]  // Zero
    [InlineData(-1)] // Negative
    public void Cut_InvalidPositions_Throws(int position)
    {
        var deck = Deck.CreateStandard();
        Assert.Throws<ArgumentOutOfRangeException>(() => deck.Cut(position));
    }

    [Fact]
    public void Cut_ChangesOrder()
    {
        var deck = Deck.CreateStandard();
        var cutDeck = deck.Cut(10, fromTop: true);

        // After cutting 10 from top, the 11th card becomes the first
        Assert.Equal(deck[10], cutDeck[0]);
    }

    [Fact]
    public void Cut_PreservesAllCards()
    {
        var deck = Deck.CreateStandard();
        var cutDeck = deck.Cut(15);

        var originalCards = deck.Cards.OrderBy(c => c.Suit).ThenBy(c => c.Rank).ToList();
        var cutCards = cutDeck.Cards.OrderBy(c => c.Suit).ThenBy(c => c.Rank).ToList();

        Assert.Equal(originalCards, cutCards);
    }

    [Fact]
    public void Deal_ReturnsCorrectCards()
    {
        var deck = Deck.CreateStandard();
        var (dealtCards, remaining) = deck.Deal(5);

        Assert.Equal(5, dealtCards.Count);
        Assert.Equal(27, remaining.Count);
    }

    [Fact]
    public void Deal_DealsFromTop()
    {
        var deck = Deck.CreateStandard();
        var (dealtCards, _) = deck.Deal(3);

        Assert.Equal(deck[0], dealtCards[0]);
        Assert.Equal(deck[1], dealtCards[1]);
        Assert.Equal(deck[2], dealtCards[2]);
    }

    [Fact]
    public void Deal_RemainingDeckStartsAfterDealtCards()
    {
        var deck = Deck.CreateStandard();
        var (_, remaining) = deck.Deal(5);

        Assert.Equal(deck[5], remaining[0]);
    }

    [Fact]
    public void Deal_TooManyCards_Throws()
    {
        var deck = Deck.CreateStandard();
        Assert.Throws<InvalidOperationException>(() => deck.Deal(33));
    }

    [Fact]
    public void Deal_NegativeCount_Throws()
    {
        var deck = Deck.CreateStandard();
        Assert.Throws<ArgumentOutOfRangeException>(() => deck.Deal(-1));
    }

    [Fact]
    public void Deal_AllCards_Succeeds()
    {
        var deck = Deck.CreateStandard();
        var (dealtCards, remaining) = deck.Deal(32);

        Assert.Equal(32, dealtCards.Count);
        Assert.Equal(0, remaining.Count);
    }

    [Fact]
    public void Deal_ZeroCards_Succeeds()
    {
        var deck = Deck.CreateStandard();
        var (dealtCards, remaining) = deck.Deal(0);

        Assert.Empty(dealtCards);
        Assert.Equal(32, remaining.Count);
    }

    [Fact]
    public void FromCards_CreatesCorrectDeck()
    {
        var cards = new[]
        {
            new Card(CardRank.Ace, CardSuit.Spades),
            new Card(CardRank.King, CardSuit.Hearts)
        };

        var deck = Deck.FromCards(cards);

        Assert.Equal(2, deck.Count);
        Assert.Equal(cards[0], deck[0]);
        Assert.Equal(cards[1], deck[1]);
    }
}
