using Giretra.Core.Cards;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a card.
/// </summary>
public sealed class CardResponse
{
    /// <summary>
    /// The card rank.
    /// </summary>
    public required CardRank Rank { get; init; }

    /// <summary>
    /// The card suit.
    /// </summary>
    public required CardSuit Suit { get; init; }

    /// <summary>
    /// String representation (e.g., "A♠").
    /// </summary>
    public required string Display { get; init; }

    public static CardResponse FromCard(Card card)
    {
        return new CardResponse
        {
            Rank = card.Rank,
            Suit = card.Suit,
            Display = card.ToString()
        };
    }
}
