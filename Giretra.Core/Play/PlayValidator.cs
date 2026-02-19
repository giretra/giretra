using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Play;

/// <summary>
/// Validates which cards can be legally played.
/// </summary>
public static class PlayValidator
{
    /// <summary>
    /// Gets all valid cards that a player can play in the current trick.
    /// </summary>
    public static IReadOnlyList<Card> GetValidPlays(
        Player player,
        TrickState trick,
        GameMode gameMode)
    {
        var hand = player.Hand;

        // First card of trick - any card is valid
        if (trick.PlayedCards.Count == 0)
        {
            return hand.ToList();
        }

        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = gameMode.GetTrumpSuit();
        var category = gameMode.GetCategory();

        // Get cards by type
        var leadSuitCards = hand.Where(c => c.Suit == leadSuit).ToList();
        var trumpCards = trumpSuit.HasValue
            ? hand.Where(c => c.Suit == trumpSuit).ToList()
            : new List<Card>();

        // Rule 1: Must follow suit if possible
        if (leadSuitCards.Count > 0)
        {
            return ApplyFollowSuitRules(leadSuitCards, trick, gameMode, category);
        }

        // Cannot follow suit - rules differ by mode
        if (category == GameModeCategory.Colour)
        {
            return ApplyColourCantFollowRules(player, hand, trick, gameMode, trumpSuit!.Value, trumpCards);
        }
        else
        {
            // SansAs/ToutAs: cannot follow, may discard any card
            return hand.ToList();
        }
    }

    private static IReadOnlyList<Card> ApplyFollowSuitRules(
        List<Card> leadSuitCards,
        TrickState trick,
        GameMode gameMode,
        GameModeCategory category)
    {
        // Must play higher if possible:
        // - ToutAs: always when following suit
        // - Colour: when following the trump suit (monter à l'atout)
        // - SansAs: no obligation to beat
        var trumpSuit = gameMode.GetTrumpSuit();
        var mustPlayHigher = category is GameModeCategory.ToutAs
                             || (category == GameModeCategory.Colour && trumpSuit.HasValue && trick.LeadSuit == trumpSuit);

        if (mustPlayHigher)
        {
            var currentHighest = GetCurrentWinningCard(trick, gameMode);

            // Must beat if possible when following suit
            if (currentHighest.HasValue && currentHighest.Value.Suit == trick.LeadSuit)
            {
                var higherCards = leadSuitCards
                    .Where(c => CardComparer.Beats(c, currentHighest.Value, trick.LeadSuit!.Value, gameMode))
                    .ToList();

                if (higherCards.Count > 0)
                {
                    return higherCards;
                }
            }
        }

        // Otherwise any card of lead suit is valid
        return leadSuitCards;
    }

    private static IReadOnlyList<Card> ApplyColourCantFollowRules(
        Player player,
        IReadOnlyList<Card> hand,
        TrickState trick,
        GameMode gameMode,
        CardSuit trumpSuit,
        List<Card> trumpCards)
    {
        var leadSuit = trick.LeadSuit!.Value;

        // Check if teammate is winning
        var (currentWinner, currentWinningCard) = GetCurrentWinner(trick, gameMode);
        var teammateWinning = currentWinner.HasValue &&
                              currentWinner.Value.GetTeam() == player.Position.GetTeam();
        var teammateWinningWithNonTrump = teammateWinning &&
                                          currentWinningCard.HasValue &&
                                          currentWinningCard.Value.Suit != trumpSuit;

        // Has trump been played in the trick?
        var trumpPlayed = trick.HasTrumpBeenPlayed(trumpSuit);

        // Rule 3: Must trump if cannot follow (unless teammate winning with non-trump)
        if (trumpCards.Count > 0)
        {
            // Exception: teammate winning with non-trump - may discard
            if (teammateWinningWithNonTrump && !trumpPlayed)
            {
                // Can play anything (trump or discard)
                return hand.ToList();
            }

            // Rule 4: Must overtrump if trump has been played
            if (trumpPlayed)
            {
                var highestTrump = trick.GetHighestTrump(trumpSuit, gameMode);
                if (highestTrump.HasValue)
                {
                    var higherTrumps = trumpCards
                        .Where(c => CardComparer.Beats(c, highestTrump.Value.Card, trumpSuit, gameMode))
                        .ToList();

                    if (higherTrumps.Count > 0)
                    {
                        return higherTrumps;
                    }
                }

                // Cannot overtrump - must play any trump
                return trumpCards;
            }

            // Trump not yet played, must play trump
            return trumpCards;
        }

        // Rule 5: No trump, discard any card
        return hand.ToList();
    }

    private static Card? GetCurrentWinningCard(TrickState trick, GameMode gameMode)
    {
        if (trick.PlayedCards.Count == 0) return null;

        var leadSuit = trick.LeadSuit!.Value;
        Card winner = trick.PlayedCards[0].Card;

        foreach (var played in trick.PlayedCards.Skip(1))
        {
            if (CardComparer.Beats(played.Card, winner, leadSuit, gameMode))
            {
                winner = played.Card;
            }
        }

        return winner;
    }

    private static (PlayerPosition? Winner, Card? WinningCard) GetCurrentWinner(
        TrickState trick,
        GameMode gameMode)
    {
        if (trick.PlayedCards.Count == 0) return (null, null);

        var leadSuit = trick.LeadSuit!.Value;
        var winningPlay = trick.PlayedCards[0];

        foreach (var played in trick.PlayedCards.Skip(1))
        {
            if (CardComparer.Beats(played.Card, winningPlay.Card, leadSuit, gameMode))
            {
                winningPlay = played;
            }
        }

        return (winningPlay.Player, winningPlay.Card);
    }

    /// <summary>
    /// Checks if a specific card is a valid play.
    /// </summary>
    public static bool IsValidPlay(
        Player player,
        Card card,
        TrickState trick,
        GameMode gameMode)
    {
        var validPlays = GetValidPlays(player, trick, gameMode);
        return validPlays.Contains(card);
    }
}
