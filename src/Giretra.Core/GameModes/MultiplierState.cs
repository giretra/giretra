namespace Giretra.Core.GameModes;

/// <summary>
/// Represents the multiplier state for match point scoring.
/// </summary>
public enum MultiplierState
{
    /// <summary>
    /// Normal scoring (×1).
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Doubled scoring (×2). Applied when opponents Double.
    /// </summary>
    Doubled = 2,

    /// <summary>
    /// Redoubled scoring (×4). Applied when announcer team counters a Double.
    /// </summary>
    Redoubled = 4
}

public static class MultiplierStateExtensions
{
    /// <summary>
    /// Gets the numeric multiplier value.
    /// </summary>
    public static int GetMultiplier(this MultiplierState state)
        => (int)state;

    /// <summary>
    /// Gets the effective multiplier considering auto-doubled modes.
    /// For modes that auto-double on accept (ColourClubs), a redouble
    /// multiplies the already-doubled value by 4, giving ×8 from base.
    /// </summary>
    public static int GetEffectiveMultiplier(this MultiplierState state, GameMode mode)
    {
        var baseMultiplier = state.GetMultiplier();
        if (mode.AcceptCausesAutoDouble() && state == MultiplierState.Redoubled)
            return baseMultiplier * 2; // 4 × 2 = 8
        return baseMultiplier;
    }
}
