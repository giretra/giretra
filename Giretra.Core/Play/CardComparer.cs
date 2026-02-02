using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Core.Play;

/// <summary>
/// Compares cards for strength in the context of a trick.
/// </summary>
public static class CardComparer
{
    /// <summary>
    /// Compares two cards in the context of a trick.
    /// Returns positive if card1 beats card2, negative if card2 beats card1, 0 if equal.
    /// </summary>
    /// <param name="card1">First card to compare.</param>
    /// <param name="card2">Second card to compare.</param>
    /// <param name="leadSuit">The suit that was led in this trick.</param>
    /// <param name="gameMode">The current game mode.</param>
    public static int Compare(Card card1, Card card2, CardSuit leadSuit, GameMode gameMode)
    {
        var trumpSuit = gameMode.GetTrumpSuit();

        // Check if either card is trump
        var card1IsTrump = trumpSuit.HasValue && card1.Suit == trumpSuit;
        var card2IsTrump = trumpSuit.HasValue && card2.Suit == trumpSuit;

        // Trump beats non-trump
        if (card1IsTrump && !card2IsTrump) return 1;
        if (card2IsTrump && !card1IsTrump) return -1;

        // Both trump or both non-trump - check if following lead
        var card1FollowsLead = card1.Suit == leadSuit;
        var card2FollowsLead = card2.Suit == leadSuit;

        // Following lead beats not following (when no trump involved)
        if (!card1IsTrump && !card2IsTrump)
        {
            if (card1FollowsLead && !card2FollowsLead) return 1;
            if (card2FollowsLead && !card1FollowsLead) return -1;
        }

        // Same suit (or both trump, or both following lead): compare strength
        if (card1.Suit == card2.Suit)
        {
            return card1.GetStrength(gameMode).CompareTo(card2.GetStrength(gameMode));
        }

        // Different non-lead, non-trump suits - no comparison (both lose to lead)
        return 0;
    }

    /// <summary>
    /// Determines if a card beats another card in the trick context.
    /// </summary>
    public static bool Beats(Card challenger, Card current, CardSuit leadSuit, GameMode gameMode)
        => Compare(challenger, current, leadSuit, gameMode) > 0;

    /// <summary>
    /// Gets the strength of a card, considering trump and mode.
    /// Higher values are stronger.
    /// </summary>
    public static int GetCardStrength(Card card, GameMode gameMode)
        => card.GetStrength(gameMode);

    /// <summary>
    /// Determines if a card is stronger than another of the same suit.
    /// </summary>
    public static bool IsStrongerInSuit(Card card1, Card card2, GameMode gameMode)
    {
        if (card1.Suit != card2.Suit)
        {
            throw new ArgumentException("Cards must be of the same suit.");
        }

        return card1.GetStrength(gameMode) > card2.GetStrength(gameMode);
    }
}
