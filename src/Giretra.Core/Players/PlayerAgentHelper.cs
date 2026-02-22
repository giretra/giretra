using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Core.Players;

/// <summary>
/// Shared helper methods for player agent implementations.
/// </summary>
public static class PlayerAgentHelper
{
    /// <summary>
    /// Determines if a card is a master card — meaning that when played, the player
    /// is guaranteed to win the trick within this suit.
    /// A card is master if every card of the same suit that is stronger than it
    /// has either already been played or is held in the player's own hand.
    /// </summary>
    public static bool IsMasterCard(Card card, GameMode mode, IReadOnlyList<Card> hand, HashSet<Card> playedCards)
    {
        int cardStrength = card.GetStrength(mode);

        foreach (CardRank rank in Enum.GetValues<CardRank>())
        {
            var potentialCard = new Card(rank, card.Suit);
            if (potentialCard.Equals(card)) continue;
            if (playedCards.Contains(potentialCard)) continue;
            if (hand.Contains(potentialCard)) continue;

            if (potentialCard.GetStrength(mode) > cardStrength)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets all master cards from the given hand.
    /// </summary>
    public static List<Card> GetMasterCards(IReadOnlyList<Card> hand, GameMode mode, HashSet<Card> playedCards)
    {
        return hand.Where(c => IsMasterCard(c, mode, hand, playedCards)).ToList();
    }
}
