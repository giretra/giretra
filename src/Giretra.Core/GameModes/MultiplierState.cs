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
}
