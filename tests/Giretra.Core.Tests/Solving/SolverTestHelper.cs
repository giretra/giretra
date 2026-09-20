using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Solving;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Solving;

internal static class SolverTestHelper
{
    /// <summary>
    /// Deals a shuffled deck and plays the given number of random valid cards.
    /// Retries until the resulting hand satisfies <paramref name="accept"/> (when given).
    /// </summary>
    public static (HandState Hand, Dictionary<PlayerPosition, List<Card>> Hands) PlayRandomly(
        GameMode mode,
        Random random,
        int cardsToPlay,
        Func<HandState, bool>? accept = null)
    {
        while (true)
        {
            var deck = Deck.CreateShuffled(random);
            var hands = Enum.GetValues<PlayerPosition>()
                .ToDictionary(p => p, p => deck.Cards.Skip((int)p * 8).Take(8).ToList());

            var hand = HandState.Create(mode, (PlayerPosition)random.Next(4));
            for (var i = 0; i < cardsToPlay; i++)
            {
                var player = hand.CurrentTrick!.CurrentPlayer!.Value;
                var valid = PlayValidator.GetValidPlays(Player.Create(player, hands[player]), hand.CurrentTrick, mode);
                var card = valid[random.Next(valid.Count)];
                hands[player].Remove(card);
                hand = hand.PlayCard(card);
            }

            if (accept is null || accept(hand))
            {
                return (hand, hands);
            }
        }
    }

    public static IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> AsReadOnly(
        Dictionary<PlayerPosition, List<Card>> hands)
        => hands.ToDictionary(h => h.Key, h => (IReadOnlyList<Card>)h.Value.ToList());

    /// <summary>
    /// Builds a hand where the given player's team has won every trick so far and the player is on lead.
    /// The played cards are placeholders; only the winners and captured points matter.
    /// </summary>
    public static HandState HandWithTricksWonBy(GameMode mode, PlayerPosition leader, int tricks)
    {
        var hand = HandState.Create(mode, leader);
        for (var i = 0; i < tricks; i++)
        {
            // The Ace beats the 7, 8 and Queen under both rankings
            hand = hand
                .PlayCard(new Card(CardRank.Ace, CardSuit.Hearts))
                .PlayCard(new Card(CardRank.Seven, CardSuit.Hearts))
                .PlayCard(new Card(CardRank.Eight, CardSuit.Hearts))
                .PlayCard(new Card(CardRank.Queen, CardSuit.Hearts));
        }

        return hand;
    }

    /// <summary>
    /// Reference minimax built on the engine's own <see cref="PlayValidator"/> and <see cref="HandState"/>:
    /// Team1's solver score after <paramref name="card"/> is played and everyone plays perfectly.
    /// Only usable near the end of a hand.
    /// </summary>
    public static int BruteForce(HandState hand, Dictionary<PlayerPosition, List<Card>> hands, Card card)
    {
        var player = hand.CurrentTrick!.CurrentPlayer!.Value;
        var next = hands.ToDictionary(h => h.Key, h => h.Value.ToList());
        next[player].Remove(card);
        return BruteForce(hand.PlayCard(card), next);
    }

    private static int BruteForce(HandState hand, Dictionary<PlayerPosition, List<Card>> hands)
    {
        if (hand.IsComplete)
        {
            var score = hand.Team1CardPoints;
            if (hand.SweepingTeam == Team.Team1) score += DoubleDummySolver.SweepBonus;
            if (hand.SweepingTeam == Team.Team2) score -= DoubleDummySolver.SweepBonus;
            return score;
        }

        var player = hand.CurrentTrick!.CurrentPlayer!.Value;
        var valid = PlayValidator.GetValidPlays(Player.Create(player, hands[player]), hand.CurrentTrick, hand.GameMode);
        var maximizing = player.GetTeam() == Team.Team1;
        var best = maximizing ? int.MinValue : int.MaxValue;

        foreach (var card in valid)
        {
            hands[player].Remove(card);
            var value = BruteForce(hand.PlayCard(card), hands);
            hands[player].Add(card);

            best = maximizing ? Math.Max(best, value) : Math.Min(best, value);
        }

        return best;
    }
}
