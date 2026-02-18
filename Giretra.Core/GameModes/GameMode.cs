using Giretra.Core.Cards;

namespace Giretra.Core.GameModes;

/// <summary>
/// Represents the six game modes, ordered from lowest to highest.
/// Hierarchy: ColourClubs < ColourDiamonds < ColourHearts < ColourSpades < SansAs < ToutAs
/// </summary>
public enum GameMode
{
    ColourClubs = 0,
    ColourDiamonds = 1,
    ColourHearts = 2,
    ColourSpades = 3,
    SansAs = 4,
    ToutAs = 5
}

public static class GameModeExtensions
{
    /// <summary>
    /// Checks if this game mode is strictly higher than another in the bidding hierarchy.
    /// </summary>
    public static bool IsHigherThan(this GameMode mode, GameMode other)
        => (int)mode > (int)other;

    /// <summary>
    /// Gets the category of this game mode.
    /// </summary>
    public static GameModeCategory GetCategory(this GameMode mode)
        => mode switch
        {
            GameMode.ColourClubs or GameMode.ColourDiamonds or
            GameMode.ColourHearts or GameMode.ColourSpades => GameModeCategory.Colour,
            GameMode.SansAs => GameModeCategory.SansAs,
            GameMode.ToutAs => GameModeCategory.ToutAs,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>
    /// Gets the trump suit for Colour modes. Returns null for SansAs/ToutAs.
    /// </summary>
    public static CardSuit? GetTrumpSuit(this GameMode mode)
        => mode switch
        {
            GameMode.ColourClubs => CardSuit.Clubs,
            GameMode.ColourDiamonds => CardSuit.Diamonds,
            GameMode.ColourHearts => CardSuit.Hearts,
            GameMode.ColourSpades => CardSuit.Spades,
            _ => null
        };

    /// <summary>
    /// Gets the minimum card points needed to win (not lose) as the announcer.
    /// </summary>
    public static int GetWinThreshold(this GameMode mode)
        => mode.GetCategory() switch
        {
            GameModeCategory.ToutAs => 129,
            GameModeCategory.SansAs => 65,
            GameModeCategory.Colour => 82,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>
    /// Gets the total card points available in this mode (including last trick bonus).
    /// </summary>
    public static int GetTotalPoints(this GameMode mode)
        => mode.GetCategory() switch
        {
            GameModeCategory.ToutAs => 258,  // 62×4 + 10
            GameModeCategory.SansAs => 130,  // 30×4 + 10
            GameModeCategory.Colour => 162,  // 62 + 30×3 + 10
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>
    /// Gets the base match points for this mode (before multipliers).
    /// </summary>
    public static int GetBaseMatchPoints(this GameMode mode)
        => mode switch
        {
            GameMode.ToutAs => 26,
            GameMode.SansAs => 52,
            GameMode.ColourClubs => 32,  // Clubs count double
            _ when mode.IsColourMode() => 16,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>
    /// Gets the sweep bonus match points for this mode.
    /// Note: Colour sweep results in instant match win (handled separately).
    /// </summary>
    public static int GetSweepBonus(this GameMode mode)
        => mode.GetCategory() switch
        {
            GameModeCategory.ToutAs => 35,
            GameModeCategory.SansAs => 90,
            GameModeCategory.Colour => 0, // Instant win, not a point bonus
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>
    /// Checks if redouble is allowed for this game mode.
    /// Redouble is NOT allowed for SansAs and ColourClubs (already implicitly doubled on accept).
    /// </summary>
    public static bool CanRedouble(this GameMode mode)
        => mode is not (GameMode.SansAs or GameMode.ColourClubs);

    /// <summary>
    /// Checks if accepting this mode by opponent causes automatic double.
    /// This applies to SansAs and ColourClubs.
    /// </summary>
    public static bool AcceptCausesAutoDouble(this GameMode mode)
        => mode is GameMode.SansAs or GameMode.ColourClubs;

    /// <summary>
    /// Creates a Colour game mode from a suit.
    /// </summary>
    public static GameMode FromSuit(CardSuit suit)
        => suit switch
        {
            CardSuit.Clubs => GameMode.ColourClubs,
            CardSuit.Diamonds => GameMode.ColourDiamonds,
            CardSuit.Hearts => GameMode.ColourHearts,
            CardSuit.Spades => GameMode.ColourSpades,
            _ => throw new ArgumentOutOfRangeException(nameof(suit))
        };

    /// <summary>
    /// Checks if this game mode is a Colour mode.
    /// </summary>
    public static bool IsColourMode(this GameMode mode)
        => mode.GetCategory() == GameModeCategory.Colour;

    /// <summary>
    /// Gets all game modes in hierarchy order.
    /// </summary>
    public static IEnumerable<GameMode> GetAllModes()
    {
        yield return GameMode.ColourClubs;
        yield return GameMode.ColourDiamonds;
        yield return GameMode.ColourHearts;
        yield return GameMode.ColourSpades;
        yield return GameMode.SansAs;
        yield return GameMode.ToutAs;
    }
}
