namespace Giretra.Core.GameModes;

/// <summary>
/// Represents the three categories of game modes.
/// </summary>
public enum GameModeCategory
{
    /// <summary>
    /// One suit is designated as trump. Four variants exist (one per suit).
    /// </summary>
    Colour,

    /// <summary>
    /// No trump suit. All suits use standard ranking (A > 10 > K > Q > J > 9 > 8 > 7).
    /// </summary>
    SansAs,

    /// <summary>
    /// No trump suit. All suits use trump ranking (J > 9 > A > 10 > K > Q > 8 > 7).
    /// </summary>
    ToutAs
}
