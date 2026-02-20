using Giretra.Core.Cards;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Breakdown of card points by card rank for a team.
/// </summary>
public sealed class CardPointsBreakdownResponse
{
    /// <summary>
    /// Points from Jacks (20 each in trump/AllTrumps, 2 each in non-trump/NoTrumps).
    /// </summary>
    public int Jacks { get; init; }

    /// <summary>
    /// Points from Nines (14 each in trump/AllTrumps, 0 in non-trump/NoTrumps).
    /// </summary>
    public int Nines { get; init; }

    /// <summary>
    /// Points from Aces (11 each).
    /// </summary>
    public int Aces { get; init; }

    /// <summary>
    /// Points from Tens (10 each).
    /// </summary>
    public int Tens { get; init; }

    /// <summary>
    /// Points from Kings (4 each).
    /// </summary>
    public int Kings { get; init; }

    /// <summary>
    /// Points from Queens (3 each).
    /// </summary>
    public int Queens { get; init; }

    /// <summary>
    /// Bonus points for winning the last trick (+10).
    /// </summary>
    public int LastTrickBonus { get; init; }

    /// <summary>
    /// Total card points (sum of all above).
    /// </summary>
    public int Total { get; init; }
}
