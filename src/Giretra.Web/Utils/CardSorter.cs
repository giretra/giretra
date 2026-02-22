using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Web.Utils;

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
    /// Sorts a hand for display, grouping by suit with trump first.
    /// If no game mode, uses AllTrumps ranking with natural suit order.
    /// </summary>
    public static IReadOnlyList<Card> SortHand(IEnumerable<Card> cards, GameMode? gameMode)
    {
        var cardList = cards.ToList();
        var trumpSuit = gameMode?.GetTrumpSuit();

        // Use AllTrumps if no game mode (for default trump-style ranking)
        var effectiveMode = gameMode ?? GameMode.AllTrumps;

        return cardList
            .OrderBy(c => GetSuitSortOrder(c.Suit, trumpSuit))
            .ThenByDescending(c => c.GetStrength(effectiveMode))
            .ToList();
    }

    private static int GetSuitSortOrder(CardSuit suit, CardSuit? trumpSuit)
    {
        // Trump suit always comes first
        if (trumpSuit.HasValue && suit == trumpSuit.Value)
            return -1;

        return Array.IndexOf(SuitOrder, suit);
    }
}
