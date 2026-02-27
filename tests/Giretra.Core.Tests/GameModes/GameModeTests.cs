using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Core.Tests.GameModes;

public class GameModeTests
{
    [Fact]
    public void IsHigherThan_FollowsHierarchy()
    {
        Assert.True(GameMode.AllTrumps.IsHigherThan(GameMode.NoTrumps));
        Assert.True(GameMode.NoTrumps.IsHigherThan(GameMode.ColourSpades));
        Assert.True(GameMode.ColourSpades.IsHigherThan(GameMode.ColourHearts));
        Assert.True(GameMode.ColourHearts.IsHigherThan(GameMode.ColourDiamonds));
        Assert.True(GameMode.ColourDiamonds.IsHigherThan(GameMode.ColourClubs));
    }

    [Fact]
    public void IsHigherThan_SameMode_ReturnsFalse()
    {
        Assert.False(GameMode.AllTrumps.IsHigherThan(GameMode.AllTrumps));
        Assert.False(GameMode.ColourSpades.IsHigherThan(GameMode.ColourSpades));
    }

    [Theory]
    [InlineData(GameMode.ColourClubs, GameModeCategory.Colour)]
    [InlineData(GameMode.ColourDiamonds, GameModeCategory.Colour)]
    [InlineData(GameMode.ColourHearts, GameModeCategory.Colour)]
    [InlineData(GameMode.ColourSpades, GameModeCategory.Colour)]
    [InlineData(GameMode.NoTrumps, GameModeCategory.NoTrumps)]
    [InlineData(GameMode.AllTrumps, GameModeCategory.AllTrumps)]
    public void GetCategory_ReturnsCorrectCategory(GameMode mode, GameModeCategory expected)
    {
        Assert.Equal(expected, mode.GetCategory());
    }

    [Theory]
    [InlineData(GameMode.ColourClubs, CardSuit.Clubs)]
    [InlineData(GameMode.ColourDiamonds, CardSuit.Diamonds)]
    [InlineData(GameMode.ColourHearts, CardSuit.Hearts)]
    [InlineData(GameMode.ColourSpades, CardSuit.Spades)]
    public void GetTrumpSuit_ColourModes_ReturnsCorrectSuit(GameMode mode, CardSuit expected)
    {
        Assert.Equal(expected, mode.GetTrumpSuit());
    }

    [Theory]
    [InlineData(GameMode.NoTrumps)]
    [InlineData(GameMode.AllTrumps)]
    public void GetTrumpSuit_NoTrumpModes_ReturnsNull(GameMode mode)
    {
        Assert.Null(mode.GetTrumpSuit());
    }

    [Theory]
    [InlineData(GameMode.AllTrumps, 129)]
    [InlineData(GameMode.NoTrumps, 65)]
    [InlineData(GameMode.ColourSpades, 82)]
    [InlineData(GameMode.ColourClubs, 82)]
    public void GetWinThreshold_ReturnsCorrectValue(GameMode mode, int expected)
    {
        Assert.Equal(expected, mode.GetWinThreshold());
    }

    [Theory]
    [InlineData(GameMode.AllTrumps, 258)]
    [InlineData(GameMode.NoTrumps, 130)]
    [InlineData(GameMode.ColourSpades, 162)]
    public void GetTotalPoints_ReturnsCorrectValue(GameMode mode, int expected)
    {
        Assert.Equal(expected, mode.GetTotalPoints());
    }

    [Theory]
    [InlineData(GameMode.AllTrumps, 26)]
    [InlineData(GameMode.NoTrumps, 26)]
    [InlineData(GameMode.ColourSpades, 16)]
    [InlineData(GameMode.ColourHearts, 16)]
    [InlineData(GameMode.ColourDiamonds, 16)]
    [InlineData(GameMode.ColourClubs, 32)]
    public void GetBaseMatchPoints_ReturnsCorrectValue(GameMode mode, int expected)
    {
        Assert.Equal(expected, mode.GetBaseMatchPoints());
    }

    [Theory]
    [InlineData(GameMode.AllTrumps, 35)]
    [InlineData(GameMode.NoTrumps, 90)]
    [InlineData(GameMode.ColourSpades, 0)] // Instant win, not points
    public void GetSweepBonus_ReturnsCorrectValue(GameMode mode, int expected)
    {
        Assert.Equal(expected, mode.GetSweepBonus());
    }

    [Theory]
    [InlineData(GameMode.AllTrumps, true)]
    [InlineData(GameMode.ColourSpades, true)]
    [InlineData(GameMode.ColourHearts, true)]
    [InlineData(GameMode.ColourDiamonds, true)]
    [InlineData(GameMode.NoTrumps, false)]
    [InlineData(GameMode.ColourClubs, true)]
    public void CanRedouble_ReturnsCorrectValue(GameMode mode, bool expected)
    {
        Assert.Equal(expected, mode.CanRedouble());
    }

    [Theory]
    [InlineData(GameMode.NoTrumps, true)]
    [InlineData(GameMode.ColourClubs, true)]
    [InlineData(GameMode.AllTrumps, false)]
    [InlineData(GameMode.ColourSpades, false)]
    public void AcceptCausesAutoDouble_ReturnsCorrectValue(GameMode mode, bool expected)
    {
        Assert.Equal(expected, mode.AcceptCausesAutoDouble());
    }

    [Theory]
    [InlineData(CardSuit.Clubs, GameMode.ColourClubs)]
    [InlineData(CardSuit.Diamonds, GameMode.ColourDiamonds)]
    [InlineData(CardSuit.Hearts, GameMode.ColourHearts)]
    [InlineData(CardSuit.Spades, GameMode.ColourSpades)]
    public void FromSuit_ReturnsCorrectMode(CardSuit suit, GameMode expected)
    {
        Assert.Equal(expected, GameModeExtensions.FromSuit(suit));
    }

    [Theory]
    [InlineData(GameMode.ColourClubs, true)]
    [InlineData(GameMode.ColourSpades, true)]
    [InlineData(GameMode.NoTrumps, false)]
    [InlineData(GameMode.AllTrumps, false)]
    public void IsColourMode_ReturnsCorrectValue(GameMode mode, bool expected)
    {
        Assert.Equal(expected, mode.IsColourMode());
    }

    [Fact]
    public void GetAllModes_ReturnsAllSixModes()
    {
        var modes = GameModeExtensions.GetAllModes().ToList();

        Assert.Equal(6, modes.Count);
        Assert.Contains(GameMode.ColourClubs, modes);
        Assert.Contains(GameMode.ColourDiamonds, modes);
        Assert.Contains(GameMode.ColourHearts, modes);
        Assert.Contains(GameMode.ColourSpades, modes);
        Assert.Contains(GameMode.NoTrumps, modes);
        Assert.Contains(GameMode.AllTrumps, modes);
    }

    [Fact]
    public void MultiplierState_GetMultiplier_ReturnsCorrectValues()
    {
        Assert.Equal(1, MultiplierState.Normal.GetMultiplier());
        Assert.Equal(2, MultiplierState.Doubled.GetMultiplier());
        Assert.Equal(4, MultiplierState.Redoubled.GetMultiplier());
    }
}
