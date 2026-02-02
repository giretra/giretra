using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.UI;

/// <summary>
/// Sorts cards for display: trump first, then by suit, then by strength within suit.
/// </summary>
public static class CardSorter
{
    /// <summary>
    /// Standard suit display order: Spades, Hearts, Diamonds, Clubs.
    /// </summary>
    private static readonly CardSuit[] SuitOrder = [CardSuit.Spades, CardSuit.Hearts, CardSuit.Diamonds, CardSuit.Clubs];

    /// <summary>
    /// Trump/ToutAs ranking: J > 9 > A > 10 > K > Q > 8 > 7
    /// </summary>
    private static readonly CardRank[] TrumpRankOrder =
    [
        CardRank.Jack, CardRank.Nine, CardRank.Ace, CardRank.Ten,
        CardRank.King, CardRank.Queen, CardRank.Eight, CardRank.Seven
    ];

    /// <summary>
    /// Non-trump/SansAs ranking: A > 10 > K > Q > J > 9 > 8 > 7
    /// </summary>
    private static readonly CardRank[] StandardRankOrder =
    [
        CardRank.Ace, CardRank.Ten, CardRank.King, CardRank.Queen,
        CardRank.Jack, CardRank.Nine, CardRank.Eight, CardRank.Seven
    ];

    /// <summary>
    /// Sorts a hand for display, grouping by suit with trump first.
    /// </summary>
    public static IReadOnlyList<Card> SortHand(IEnumerable<Card> cards, GameMode? gameMode)
    {
        var cardList = cards.ToList();
        var trumpSuit = gameMode?.GetTrumpSuit();

        return cardList
            .OrderBy(c => GetSuitSortOrder(c.Suit, trumpSuit))
            .ThenBy(c => GetRankSortOrder(c.Rank, gameMode, c.Suit == trumpSuit))
            .ToList();
    }

    /// <summary>
    /// Groups cards by suit for display, with trump suit first.
    /// </summary>
    public static IReadOnlyList<(CardSuit? Suit, IReadOnlyList<Card> Cards)> GroupBySuit(
        IEnumerable<Card> cards,
        GameMode? gameMode)
    {
        var sortedCards = SortHand(cards, gameMode);
        var trumpSuit = gameMode?.GetTrumpSuit();

        var groups = new List<(CardSuit? Suit, IReadOnlyList<Card> Cards)>();

        // Group trump separately if in Colour mode
        if (trumpSuit.HasValue)
        {
            var trumpCards = sortedCards.Where(c => c.Suit == trumpSuit.Value).ToList();
            if (trumpCards.Count > 0)
            {
                groups.Add((null, trumpCards)); // null indicates "TRUMP" group
            }
        }

        // Add other suits in order
        foreach (var suit in SuitOrder)
        {
            if (trumpSuit.HasValue && suit == trumpSuit.Value)
                continue; // Already added as trump

            var suitCards = sortedCards.Where(c => c.Suit == suit).ToList();
            if (suitCards.Count > 0)
            {
                groups.Add((suit, suitCards));
            }
        }

        return groups;
    }

    private static int GetSuitSortOrder(CardSuit suit, CardSuit? trumpSuit)
    {
        // Trump suit always comes first
        if (trumpSuit.HasValue && suit == trumpSuit.Value)
            return -1;

        return Array.IndexOf(SuitOrder, suit);
    }

    private static int GetRankSortOrder(CardRank rank, GameMode? gameMode, bool isTrump)
    {
        var category = gameMode?.GetCategory() ?? GameModeCategory.Colour;
        var useTrumpRanking = category == GameModeCategory.ToutAs || isTrump;
        var rankOrder = useTrumpRanking ? TrumpRankOrder : StandardRankOrder;

        return Array.IndexOf(rankOrder, rank);
    }
}
