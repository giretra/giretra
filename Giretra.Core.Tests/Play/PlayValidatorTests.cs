using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Play;

public class PlayValidatorTests
{
    [Fact]
    public void FirstCard_AnyCardIsValid()
    {
        var player = Player.Create(PlayerPosition.Bottom, new[]
        {
            new Card(CardRank.Ace, CardSuit.Hearts),
            new Card(CardRank.Seven, CardSuit.Spades),
            new Card(CardRank.Jack, CardSuit.Clubs)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1);

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        Assert.Equal(3, validPlays.Count);
    }

    [Fact]
    public void MustFollowSuit_WhenHoldingLeadSuit()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Ace, CardSuit.Hearts),
            new Card(CardRank.Seven, CardSuit.Hearts),
            new Card(CardRank.Jack, CardSuit.Clubs)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.King, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        Assert.Equal(2, validPlays.Count);
        Assert.All(validPlays, c => Assert.Equal(CardSuit.Hearts, c.Suit));
    }

    [Fact]
    public void ToutAs_MustBeatIfPossible()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Ace, CardSuit.Hearts),
            new Card(CardRank.Nine, CardSuit.Hearts),  // Higher than Ten in ToutAs
            new Card(CardRank.Seven, CardSuit.Hearts)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ten, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ToutAs);

        // In ToutAs: J > 9 > A > 10, so Nine and Ace beat Ten
        Assert.Equal(2, validPlays.Count);
        Assert.Contains(new Card(CardRank.Nine, CardSuit.Hearts), validPlays);
        Assert.Contains(new Card(CardRank.Ace, CardSuit.Hearts), validPlays);
    }

    [Fact]
    public void SansAs_MustBeatIfPossible()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Ace, CardSuit.Hearts),
            new Card(CardRank.King, CardSuit.Hearts),
            new Card(CardRank.Seven, CardSuit.Hearts)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Queen, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.SansAs);

        // In SansAs: A > 10 > K > Q, so Ace and King beat Queen
        Assert.Equal(2, validPlays.Count);
        Assert.Contains(new Card(CardRank.Ace, CardSuit.Hearts), validPlays);
        Assert.Contains(new Card(CardRank.King, CardSuit.Hearts), validPlays);
    }

    [Fact]
    public void SansAs_CannotBeat_PlayAnyLeadSuitCard()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Queen, CardSuit.Hearts),
            new Card(CardRank.Seven, CardSuit.Hearts),
            new Card(CardRank.Jack, CardSuit.Clubs)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.SansAs);

        // Cannot beat Ace, so can play any heart
        Assert.Equal(2, validPlays.Count);
        Assert.All(validPlays, c => Assert.Equal(CardSuit.Hearts, c.Suit));
    }

    [Fact]
    public void Colour_MustTrump_WhenCannotFollowAndHasTrump()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Seven, CardSuit.Spades),  // Trump
            new Card(CardRank.Ace, CardSuit.Clubs),
            new Card(CardRank.King, CardSuit.Diamonds)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // Must play trump (Spades)
        Assert.Single(validPlays);
        Assert.Equal(new Card(CardRank.Seven, CardSuit.Spades), validPlays[0]);
    }

    [Fact]
    public void Colour_MayDiscard_WhenTeammateWinningWithNonTrump()
    {
        var player = Player.Create(PlayerPosition.Top, new[]
        {
            new Card(CardRank.Seven, CardSuit.Spades),  // Trump
            new Card(CardRank.Ace, CardSuit.Clubs),
            new Card(CardRank.King, CardSuit.Diamonds)
        });

        // Bottom (teammate) leads Ace of Hearts, Left plays Seven of Hearts
        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts))  // Bottom leads
            .PlayCard(new Card(CardRank.Seven, CardSuit.Hearts));  // Left follows

        // Top (teammate of Bottom) - Bottom is winning with non-trump
        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // Can play anything (trump or discard) since teammate winning with non-trump
        Assert.Equal(3, validPlays.Count);
    }

    [Fact]
    public void Colour_MustOvertrump_WhenTrumpPlayed()
    {
        var player = Player.Create(PlayerPosition.Top, new[]
        {
            new Card(CardRank.Jack, CardSuit.Spades),  // Higher trump
            new Card(CardRank.Seven, CardSuit.Spades), // Lower trump
            new Card(CardRank.Ace, CardSuit.Clubs)
        });

        // Hearts led, Left plays trump (9 of Spades)
        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts))
            .PlayCard(new Card(CardRank.Nine, CardSuit.Spades));  // Left trumps

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // Must overtrump with Jack (only trump that beats Nine)
        Assert.Single(validPlays);
        Assert.Equal(new Card(CardRank.Jack, CardSuit.Spades), validPlays[0]);
    }

    [Fact]
    public void Colour_CannotOvertrump_PlayAnyTrump()
    {
        var player = Player.Create(PlayerPosition.Top, new[]
        {
            new Card(CardRank.Eight, CardSuit.Spades),  // Lower trump
            new Card(CardRank.Seven, CardSuit.Spades),  // Lower trump
            new Card(CardRank.Ace, CardSuit.Clubs)
        });

        // Hearts led, Left plays Jack of Spades (highest trump)
        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts))
            .PlayCard(new Card(CardRank.Jack, CardSuit.Spades));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // Cannot overtrump, must play any trump
        Assert.Equal(2, validPlays.Count);
        Assert.All(validPlays, c => Assert.Equal(CardSuit.Spades, c.Suit));
    }

    [Fact]
    public void Colour_NoTrump_DiscardAny()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Ace, CardSuit.Clubs),
            new Card(CardRank.King, CardSuit.Diamonds)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts));

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // No hearts, no trumps (spades) - can discard any
        Assert.Equal(2, validPlays.Count);
    }

    [Fact]
    public void IsValidPlay_ReturnsTrueForValidCard()
    {
        var player = Player.Create(PlayerPosition.Left, new[]
        {
            new Card(CardRank.Ace, CardSuit.Hearts),
            new Card(CardRank.Seven, CardSuit.Clubs)
        });

        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.King, CardSuit.Hearts));

        Assert.True(PlayValidator.IsValidPlay(player, new Card(CardRank.Ace, CardSuit.Hearts), trick, GameMode.SansAs));
        Assert.False(PlayValidator.IsValidPlay(player, new Card(CardRank.Seven, CardSuit.Clubs), trick, GameMode.SansAs));
    }

    [Fact]
    public void Colour_MustOvertrumpEvenIfTeammatePlayed()
    {
        var player = Player.Create(PlayerPosition.Right, new[]
        {
            new Card(CardRank.Jack, CardSuit.Spades),  // Higher trump
            new Card(CardRank.Seven, CardSuit.Spades), // Lower trump
            new Card(CardRank.Ace, CardSuit.Clubs)
        });

        // Hearts led, then two plays, then Left (Right's teammate) trumps
        var trick = TrickState.Create(PlayerPosition.Bottom, 1)
            .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts))  // Bottom leads
            .PlayCard(new Card(CardRank.Nine, CardSuit.Spades)) // Left trumps
            .PlayCard(new Card(CardRank.King, CardSuit.Hearts)); // Top follows

        var validPlays = PlayValidator.GetValidPlays(player, trick, GameMode.ColourSpades);

        // Must still overtrump even though teammate played the trump
        Assert.Single(validPlays);
        Assert.Equal(new Card(CardRank.Jack, CardSuit.Spades), validPlays[0]);
    }
}
