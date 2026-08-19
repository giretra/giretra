namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to submit a cut decision.
/// </summary>
public sealed class CutRequest
{
    /// <summary>
    /// Client ID of the player making the cut.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The number of cards to cut (6-26).
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Whether to cut from the top of the deck.
    /// </summary>
    public bool FromTop { get; init; } = true;
}
