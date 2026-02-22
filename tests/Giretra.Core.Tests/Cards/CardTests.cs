using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Core.Tests.Cards;

public class CardTests
{
    [Fact]
    public void Card_Equality_WorksCorrectly()
    {
        var card1 = new Card(CardRank.Ace, CardSuit.Spades);
        var card2 = new Card(CardRank.Ace, CardSuit.Spades);
        var card3 = new Card(CardRank.King, CardSuit.Spades);

        Assert.Equal(card1, card2);
        Assert.NotEqual(card1, card3);
    }

    [Theory]
    [InlineData(CardRank.Jack, 20)]
    [InlineData(CardRank.Nine, 14)]
    [InlineData(CardRank.Ace, 11)]
    [InlineData(CardRank.Ten, 10)]
    [InlineData(CardRank.King, 4)]
    [InlineData(CardRank.Queen, 3)]
    [InlineData(CardRank.Eight, 0)]
    [InlineData(CardRank.Seven, 0)]
    public void GetPointValue_AllTrumps_ReturnsCorrectValue(CardRank rank, int expectedPoints)
    {
        var card = new Card(rank, CardSuit.Hearts);
        Assert.Equal(expectedPoints, card.GetPointValue(GameMode.AllTrumps));
    }

    [Theory]
    [InlineData(CardRank.Jack, 20)]
    [InlineData(CardRank.Nine, 14)]
    [InlineData(CardRank.Ace, 11)]
    [InlineData(CardRank.Ten, 10)]
    [InlineData(CardRank.King, 4)]
    [InlineData(CardRank.Queen, 3)]
    [InlineData(CardRank.Eight, 0)]
    [InlineData(CardRank.Seven, 0)]
    public void GetPointValue_TrumpSuit_ReturnsCorrectValue(CardRank rank, int expectedPoints)
    {
        var card = new Card(rank, CardSuit.Hearts);
        Assert.Equal(expectedPoints, card.GetPointValue(GameMode.ColourHearts));
    }

    [Theory]
    [InlineData(CardRank.Ace, 11)]
    [InlineData(CardRank.Ten, 10)]
    [InlineData(CardRank.King, 4)]
    [InlineData(CardRank.Queen, 3)]
    [InlineData(CardRank.Jack, 2)]
    [InlineData(CardRank.Nine, 0)]
    [InlineData(CardRank.Eight, 0)]
    [InlineData(CardRank.Seven, 0)]
    public void GetPointValue_NoTrumps_ReturnsCorrectValue(CardRank rank, int expectedPoints)
    {
        var card = new Card(rank, CardSuit.Hearts);
        Assert.Equal(expectedPoints, card.GetPointValue(GameMode.NoTrumps));
    }

    [Theory]
    [InlineData(CardRank.Ace, 11)]
    [InlineData(CardRank.Ten, 10)]
    [InlineData(CardRank.King, 4)]
    [InlineData(CardRank.Queen, 3)]
    [InlineData(CardRank.Jack, 2)]
    [InlineData(CardRank.Nine, 0)]
    [InlineData(CardRank.Eight, 0)]
    [InlineData(CardRank.Seven, 0)]
    public void GetPointValue_NonTrumpSuit_ReturnsCorrectValue(CardRank rank, int expectedPoints)
    {
        // Hearts is non-trump when Spades is trump
        var card = new Card(rank, CardSuit.Hearts);
        Assert.Equal(expectedPoints, card.GetPointValue(GameMode.ColourSpades));
    }

    [Fact]
    public void GetStrength_TrumpRanking_JackIsHighest()
    {
        var jack = new Card(CardRank.Jack, CardSuit.Hearts);
        var nine = new Card(CardRank.Nine, CardSuit.Hearts);
        var ace = new Card(CardRank.Ace, CardSuit.Hearts);

        var jackStrength = jack.GetStrength(GameMode.AllTrumps);
        var nineStrength = nine.GetStrength(GameMode.AllTrumps);
        var aceStrength = ace.GetStrength(GameMode.AllTrumps);

        Assert.True(jackStrength > nineStrength);
        Assert.True(nineStrength > aceStrength);
    }

    [Fact]
    public void GetStrength_NonTrumpRanking_AceIsHighest()
    {
        var ace = new Card(CardRank.Ace, CardSuit.Hearts);
        var ten = new Card(CardRank.Ten, CardSuit.Hearts);
        var jack = new Card(CardRank.Jack, CardSuit.Hearts);

        var aceStrength = ace.GetStrength(GameMode.NoTrumps);
        var tenStrength = ten.GetStrength(GameMode.NoTrumps);
        var jackStrength = jack.GetStrength(GameMode.NoTrumps);

        Assert.True(aceStrength > tenStrength);
        Assert.True(tenStrength > jackStrength);
    }

    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        var aceOfSpades = new Card(CardRank.Ace, CardSuit.Spades);
        var tenOfHearts = new Card(CardRank.Ten, CardSuit.Hearts);

        Assert.Equal("A♠", aceOfSpades.ToString());
        Assert.Equal("10♥", tenOfHearts.ToString());
    }

    [Fact]
    public void TotalCardPoints_AllTrumps_Is258()
    {
        var deck = Deck.CreateStandard();
        var total = deck.Cards.Sum(c => c.GetPointValue(GameMode.AllTrumps));
        // 62 × 4 = 248, plus 10 for last trick = 258
        // But last trick bonus is applied during play, not to cards
        Assert.Equal(248, total);
    }

    [Fact]
    public void TotalCardPoints_NoTrumps_Is120()
    {
        var deck = Deck.CreateStandard();
        var total = deck.Cards.Sum(c => c.GetPointValue(GameMode.NoTrumps));
        // 30 × 4 = 120, plus 10 for last trick = 130
        Assert.Equal(120, total);
    }

    [Fact]
    public void TotalCardPoints_ColourHearts_Is152()
    {
        var deck = Deck.CreateStandard();
        var total = deck.Cards.Sum(c => c.GetPointValue(GameMode.ColourHearts));
        // 62 (trump) + 30 × 3 (non-trump) = 152, plus 10 for last trick = 162
        Assert.Equal(152, total);
    }
}
