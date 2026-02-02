using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;

namespace Giretra.Core.Tests.Cards;

public class CardComparerTests
{
    [Fact]
    public void Compare_TrumpBeatNonTrump()
    {
        var trump = new Card(CardRank.Seven, CardSuit.Hearts);
        var nonTrump = new Card(CardRank.Ace, CardSuit.Spades);

        var result = CardComparer.Compare(trump, nonTrump, CardSuit.Spades, GameMode.ColourHearts);

        Assert.True(result > 0);
    }

    [Fact]
    public void Compare_HigherTrumpBeatsLowerTrump()
    {
        var jackTrump = new Card(CardRank.Jack, CardSuit.Hearts);
        var nineTrump = new Card(CardRank.Nine, CardSuit.Hearts);

        var result = CardComparer.Compare(jackTrump, nineTrump, CardSuit.Hearts, GameMode.ColourHearts);

        Assert.True(result > 0);
    }

    [Fact]
    public void Compare_TrumpRanking_JackBeatNine()
    {
        var jack = new Card(CardRank.Jack, CardSuit.Spades);
        var nine = new Card(CardRank.Nine, CardSuit.Spades);

        Assert.True(CardComparer.Beats(jack, nine, CardSuit.Spades, GameMode.ColourSpades));
        Assert.False(CardComparer.Beats(nine, jack, CardSuit.Spades, GameMode.ColourSpades));
    }

    [Fact]
    public void Compare_TrumpRanking_NineBeatAce()
    {
        var nine = new Card(CardRank.Nine, CardSuit.Spades);
        var ace = new Card(CardRank.Ace, CardSuit.Spades);

        Assert.True(CardComparer.Beats(nine, ace, CardSuit.Spades, GameMode.ColourSpades));
    }

    [Fact]
    public void Compare_NonTrumpRanking_AceBeatTen()
    {
        var ace = new Card(CardRank.Ace, CardSuit.Hearts);
        var ten = new Card(CardRank.Ten, CardSuit.Hearts);

        // In ColourSpades, hearts is non-trump
        Assert.True(CardComparer.Beats(ace, ten, CardSuit.Hearts, GameMode.ColourSpades));
    }

    [Fact]
    public void Compare_NonTrumpRanking_TenBeatKing()
    {
        var ten = new Card(CardRank.Ten, CardSuit.Hearts);
        var king = new Card(CardRank.King, CardSuit.Hearts);

        Assert.True(CardComparer.Beats(ten, king, CardSuit.Hearts, GameMode.SansAs));
    }

    [Fact]
    public void Compare_FollowingLeadBeatsNotFollowing()
    {
        var leadCard = new Card(CardRank.Seven, CardSuit.Hearts);
        var offSuit = new Card(CardRank.Ace, CardSuit.Clubs);

        // In SansAs (no trump), following lead beats not following
        var result = CardComparer.Compare(leadCard, offSuit, CardSuit.Hearts, GameMode.SansAs);

        Assert.True(result > 0);
    }

    [Fact]
    public void Compare_ToutAs_AllSuitsUseTrumpRanking()
    {
        var jackHearts = new Card(CardRank.Jack, CardSuit.Hearts);
        var aceHearts = new Card(CardRank.Ace, CardSuit.Hearts);

        // In ToutAs, Jack beats Ace in any suit
        Assert.True(CardComparer.Beats(jackHearts, aceHearts, CardSuit.Hearts, GameMode.ToutAs));
    }

    [Fact]
    public void Compare_SansAs_AllSuitsUseNonTrumpRanking()
    {
        var aceHearts = new Card(CardRank.Ace, CardSuit.Hearts);
        var jackHearts = new Card(CardRank.Jack, CardSuit.Hearts);

        // In SansAs, Ace beats Jack
        Assert.True(CardComparer.Beats(aceHearts, jackHearts, CardSuit.Hearts, GameMode.SansAs));
    }

    [Theory]
    [InlineData(CardRank.Jack, CardRank.Nine)]
    [InlineData(CardRank.Nine, CardRank.Ace)]
    [InlineData(CardRank.Ace, CardRank.Ten)]
    [InlineData(CardRank.Ten, CardRank.King)]
    [InlineData(CardRank.King, CardRank.Queen)]
    [InlineData(CardRank.Queen, CardRank.Eight)]
    [InlineData(CardRank.Eight, CardRank.Seven)]
    public void Compare_TrumpRankingOrder(CardRank higher, CardRank lower)
    {
        var higherCard = new Card(higher, CardSuit.Spades);
        var lowerCard = new Card(lower, CardSuit.Spades);

        Assert.True(CardComparer.Beats(higherCard, lowerCard, CardSuit.Spades, GameMode.ColourSpades));
    }

    [Theory]
    [InlineData(CardRank.Ace, CardRank.Ten)]
    [InlineData(CardRank.Ten, CardRank.King)]
    [InlineData(CardRank.King, CardRank.Queen)]
    [InlineData(CardRank.Queen, CardRank.Jack)]
    [InlineData(CardRank.Jack, CardRank.Nine)]
    [InlineData(CardRank.Nine, CardRank.Eight)]
    [InlineData(CardRank.Eight, CardRank.Seven)]
    public void Compare_NonTrumpRankingOrder(CardRank higher, CardRank lower)
    {
        var higherCard = new Card(higher, CardSuit.Hearts);
        var lowerCard = new Card(lower, CardSuit.Hearts);

        Assert.True(CardComparer.Beats(higherCard, lowerCard, CardSuit.Hearts, GameMode.SansAs));
    }

    [Fact]
    public void IsStrongerInSuit_SameSuitComparison()
    {
        var ace = new Card(CardRank.Ace, CardSuit.Hearts);
        var king = new Card(CardRank.King, CardSuit.Hearts);

        Assert.True(CardComparer.IsStrongerInSuit(ace, king, GameMode.SansAs));
        Assert.False(CardComparer.IsStrongerInSuit(king, ace, GameMode.SansAs));
    }

    [Fact]
    public void IsStrongerInSuit_DifferentSuits_Throws()
    {
        var hearts = new Card(CardRank.Ace, CardSuit.Hearts);
        var spades = new Card(CardRank.Ace, CardSuit.Spades);

        Assert.Throws<ArgumentException>(() => CardComparer.IsStrongerInSuit(hearts, spades, GameMode.SansAs));
    }
}
