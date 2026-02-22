using Giretra.Core.Cards;

namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to play a card.
/// </summary>
public sealed class PlayCardRequest
{
    /// <summary>
    /// Client ID of the player playing the card.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The card rank.
    /// </summary>
    public required CardRank Rank { get; init; }

    /// <summary>
    /// The card suit.
    /// </summary>
    public required CardSuit Suit { get; init; }
}
