using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Core.Players;

/// <summary>
/// The strength of a hand in a given game mode.
/// </summary>
/// <param name="GuaranteedTricks">Tricks the hand should win regardless of how the cards lie.</param>
/// <param name="ProbableTricks">Tricks the hand wins when the cards lie reasonably.</param>
/// <param name="Score">Composite strength from 0 to 100, comparable across modes.</param>
public readonly record struct HandEvaluation(int GuaranteedTricks, int ProbableTricks, double Score);

/// <summary>
/// Scores a hand in a given game mode by counting the tricks it should win. Shared by the agents
/// that decide what to announce, and by cut selection, which needs the same measure of what makes
/// a hand worth having.
/// </summary>
public static class HandEvaluator
{
    /// <summary>
    /// Evaluates a hand in the given mode.
    /// </summary>
    /// <param name="hand">The cards held. Five during negotiation, eight once fully dealt.</param>
    /// <param name="mode">The mode to evaluate for.</param>
    /// <param name="isStarter">Whether this player leads the first trick, which is worth extra in NoTrumps.</param>
    public static HandEvaluation Evaluate(IReadOnlyList<Card> hand, GameMode mode, bool isStarter)
    {
        return mode.GetCategory() switch
        {
            GameModeCategory.Colour => EvaluateColour(hand, mode),
            GameModeCategory.NoTrumps => EvaluateNoTrumps(hand, mode, isStarter),
            GameModeCategory.AllTrumps => EvaluateAllTrumps(hand, mode),
            _ => new HandEvaluation(0, 0, 0)
        };
    }

    private static HandEvaluation EvaluateColour(IReadOnlyList<Card> hand, GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit()!.Value;
        var trumpCards = hand.Where(c => c.Suit == trumpSuit).ToList();
        var sideCards = hand.Where(c => c.Suit != trumpSuit).ToList();

        int guaranteed = 0;
        int probable = 0;

        bool hasJ = trumpCards.Any(c => c.Rank == CardRank.Jack);
        bool has9 = trumpCards.Any(c => c.Rank == CardRank.Nine);
        bool hasA = trumpCards.Any(c => c.Rank == CardRank.Ace);

        if (hasJ) guaranteed++;
        if (has9)
        {
            if (hasJ) guaranteed++;
            else probable++;
        }
        if (hasA)
        {
            if (hasJ && has9) guaranteed++;
            else probable++;
        }

        // Side suits
        var sideSuits = sideCards.GroupBy(c => c.Suit).ToList();
        foreach (var suitGroup in sideSuits)
        {
            var cards = suitGroup.ToList();
            bool hasSideAce = cards.Any(c => c.Rank == CardRank.Ace);
            bool hasSideTen = cards.Any(c => c.Rank == CardRank.Ten);

            if (hasSideAce && hasSideTen)
                guaranteed++;
            else if (hasSideAce)
                probable++;
        }

        // Void side suit with >= 2 trumps = ruffing opportunity
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            if (suit == trumpSuit) continue;
            if (!hand.Any(c => c.Suit == suit) && trumpCards.Count >= 2)
                probable++;
        }

        // Long trump bonus
        if (trumpCards.Count >= 5) guaranteed++;
        else if (trumpCards.Count >= 4) probable++;

        // Composite score
        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.30
                     + trumpCards.Count * 5
                     + sideSuits.Count(g => g.Count() == 0) * 4;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }

    private static HandEvaluation EvaluateNoTrumps(IReadOnlyList<Card> hand, GameMode mode, bool isStarter)
    {
        int guaranteed = 0;
        int probable = 0;

        foreach (var suitGroup in hand.GroupBy(c => c.Suit))
        {
            var cards = suitGroup.OrderByDescending(c => c.GetStrength(mode)).ToList();
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);
            bool hasTen = cards.Any(c => c.Rank == CardRank.Ten);
            bool hasKing = cards.Any(c => c.Rank == CardRank.King);

            if (hasAce)
            {
                guaranteed++;
                if (hasTen)
                {
                    guaranteed++;
                    if (isStarter && (hasKing || suitGroup.Count() >= 4))
                        guaranteed++;
                }
            }

            if (cards.Count >= 3)
                probable++;
        }

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.15
                     + hand.Count(c => c.Rank == CardRank.Ace) * 5;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }

    private static HandEvaluation EvaluateAllTrumps(IReadOnlyList<Card> hand, GameMode mode)
    {
        int guaranteed = 0;
        int probable = 0;

        foreach (var suitGroup in hand.GroupBy(c => c.Suit))
        {
            var cards = suitGroup.ToList();
            bool hasJack = cards.Any(c => c.Rank == CardRank.Jack);
            bool hasNine = cards.Any(c => c.Rank == CardRank.Nine);
            bool hasAce = cards.Any(c => c.Rank == CardRank.Ace);

            if (hasJack)
            {
                guaranteed++;
                if (hasNine) guaranteed++;
                if (hasAce) probable++;
            }
            else if (hasNine)
            {
                probable++;
            }
        }

        int handPoints = hand.Sum(c => c.GetPointValue(mode));
        int totalPoints = mode.GetTotalPoints();
        double rawPointPercentage = (double)handPoints / totalPoints * 100;

        double score = guaranteed * 18
                     + probable * 8
                     + rawPointPercentage * 0.30
                     + hand.Count(c => c.Rank == CardRank.Jack) * 5
                     + hand.Count(c => c.Rank == CardRank.Nine) * 3;

        return new HandEvaluation(guaranteed, probable, Math.Clamp(score, 0, 100));
    }
}
