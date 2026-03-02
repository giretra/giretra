using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// Shared helper methods for player agent implementations.
/// </summary>
public static class PlayerAgentHelper
{
    /// <summary>
    /// All 32 cards in a standard Belote deck.
    /// </summary>
    public static readonly Card[] AllCards = BuildAllCards();

    private static Card[] BuildAllCards()
    {
        var cards = new Card[32];
        int i = 0;
        foreach (var suit in Enum.GetValues<CardSuit>())
        foreach (var rank in Enum.GetValues<CardRank>())
            cards[i++] = new Card(rank, suit);
        return cards;
    }

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

    public static bool IsMasterCardExcludeTrump(Card card, GameMode mode, IReadOnlyList<Card> hand, HashSet<Card> playedCards)
    {
        if (!mode.IsColourMode())
            return IsMasterCard(card, mode, hand, playedCards);

        var tempPlayedCards = playedCards.ToHashSet();

        foreach (var trumpCard in Deck.CreateStandard().Cards.ToList().Where(t => t.Suit == mode.GetTrumpSuit()!.Value))
        {
            tempPlayedCards.Add(trumpCard);
        }

        return IsMasterCard(card, mode, hand, tempPlayedCards);
    }

    /// <summary>
    /// Gets all master cards from the given hand.
    /// </summary>
    public static List<Card> GetMasterCards(IReadOnlyList<Card> hand, GameMode mode, HashSet<Card> playedCards)
    {
        return hand.Where(c => IsMasterCard(c, mode, hand, playedCards)).ToList();
    }

    /// <summary>
    /// Determines the current winning player and card in a trick.
    /// </summary>
    public static (PlayerPosition? winner, Card? card) DetermineCurrentWinner(TrickState trick, GameMode mode)
    {
        if (trick.PlayedCards.Count == 0)
            return (null, null);

        var leadSuit = trick.LeadSuit!.Value;
        PlayedCard best = trick.PlayedCards[0];
        foreach (var played in trick.PlayedCards.Skip(1))
        {
            if (CardComparer.Beats(played.Card, best.Card, leadSuit, mode))
                best = played;
        }
        return (best.Player, best.Card);
    }

    /// <summary>
    /// Gets the current winning card in a trick.
    /// </summary>
    public static Card? GetCurrentWinningCard(TrickState trick, GameMode mode)
    {
        var (_, card) = DetermineCurrentWinner(trick, mode);
        return card;
    }

    /// <summary>
    /// Finds the cheapest card from <paramref name="validPlays"/> that beats the
    /// <paramref name="currentWinner"/>, or null if none can win.
    /// </summary>
    public static Card? FindMinimumWinningCard(
        IReadOnlyList<Card> validPlays, Card currentWinner, CardSuit leadSuit, GameMode mode)
    {
        Card? best = null;
        int bestStrength = int.MaxValue;

        foreach (var card in validPlays)
        {
            if (!CardComparer.Beats(card, currentWinner, leadSuit, mode))
                continue;

            int strength = card.GetStrength(mode);
            if (strength < bestStrength)
            {
                best = card;
                bestStrength = strength;
            }
        }

        return best;
    }

    /// <summary>
    /// Sums the point value of all cards played so far in a trick.
    /// </summary>
    public static int GetTrickPointsSoFar(TrickState trick, GameMode mode)
    {
        return trick.PlayedCards.Sum(pc => pc.Card.GetPointValue(mode));
    }

    /// <summary>
    /// Returns 0.0 (conservative) to 1.0 (desperate/aggressive) based on match score.
    /// </summary>
    public static double ComputeAggressiveness(int ourPoints, int theirPoints, int targetScore)
    {
        double ourProgress = (double)ourPoints / targetScore;
        double theirProgress = (double)theirPoints / targetScore;

        if (theirProgress >= 0.8 && ourProgress < 0.5)
            return 0.9;
        if (theirProgress >= 0.6 && ourProgress < 0.3)
            return 0.7;

        if (ourProgress >= 0.8)
            return 0.0;
        if (ourProgress >= 0.6)
            return 0.1;

        return 0.3;
    }
}
