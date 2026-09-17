using Giretra.Core.Cards;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Solving;

/// <summary>
/// Post-game analysis: replays a hand card by card and measures every play against the solver.
/// </summary>
public static class HandAnalyzer
{
    /// <summary>
    /// Analyses a completed hand. The four starting hands are rebuilt from the cards each player played.
    /// </summary>
    /// <param name="completedHand">The hand after all 8 tricks.</param>
    /// <param name="scoring">The negotiation outcome; when given, losses are also measured in match points.</param>
    /// <param name="solver">An existing solver for the hand's game mode to reuse (optional).</param>
    public static HandAnalysis Analyze(
        HandState completedHand,
        SolverScoring? scoring = null,
        DoubleDummySolver? solver = null)
    {
        if (!completedHand.IsComplete)
        {
            throw new ArgumentException(
                "The hand is not complete; pass the starting hands to analyse a hand in progress.",
                nameof(completedHand));
        }

        var startingHands = GetPlays(completedHand)
            .GroupBy(p => p.Player)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Card>)g.Select(p => p.Card).ToList());

        return Analyze(completedHand, startingHands, scoring, solver);
    }

    /// <summary>
    /// Analyses the plays made so far in a hand, complete or not.
    /// </summary>
    /// <param name="hand">The hand to analyse.</param>
    /// <param name="startingHands">The 8 cards each player held before the first trick.</param>
    /// <param name="scoring">The negotiation outcome; when given, losses are also measured in match points.</param>
    /// <param name="solver">An existing solver for the hand's game mode to reuse (optional).</param>
    public static HandAnalysis Analyze(
        HandState hand,
        IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> startingHands,
        SolverScoring? scoring = null,
        DoubleDummySolver? solver = null)
    {
        solver ??= new DoubleDummySolver(hand.GameMode);

        var firstLeader = hand.CompletedTricks.Count > 0
            ? hand.CompletedTricks[0].Leader
            : hand.CurrentTrick!.Leader;

        var remaining = startingHands.ToDictionary(h => h.Key, h => h.Value.ToList());
        IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> Snapshot()
            => remaining.ToDictionary(h => h.Key, h => (IReadOnlyList<Card>)h.Value);

        var replay = HandState.Create(hand.GameMode, firstLeader);
        var par = solver.Solve(replay, Snapshot());
        var plays = new List<PlayAnalysis>();

        foreach (var played in GetPlays(hand))
        {
            var trick = replay.CurrentTrick!;
            if (trick.CurrentPlayer != played.Player)
            {
                throw new ArgumentException($"Expected {trick.CurrentPlayer} to play, not {played.Player}.", nameof(hand));
            }

            if (!remaining[played.Player].Contains(played.Card))
            {
                throw new ArgumentException($"{played.Player} does not hold {played.Card}.", nameof(startingHands));
            }

            var evaluation = solver.Evaluate(replay, Snapshot());
            var chosen = evaluation.GetCard(played.Card);

            plays.Add(new PlayAnalysis(
                trick.TrickNumber,
                played.Player,
                played.Card,
                evaluation,
                chosen.CardPointLoss,
                scoring is null ? null : evaluation.GetMatchPointLoss(played.Card, scoring),
                chosen.IsOptimal,
                evaluation.Cards.Count == 1));

            remaining[played.Player].Remove(played.Card);
            replay = replay.PlayCard(played.Card);
        }

        // The result the table is heading to: the actual one if the hand is over, else perfect play from here
        var final = replay.IsComplete
            ? new SolvedOutcome(replay.Team1CardPoints, replay.Team2CardPoints, replay.SweepingTeam)
            : solver.Solve(replay, Snapshot());

        return new HandAnalysis(hand.GameMode, scoring, par, final, plays);
    }

    private static IEnumerable<PlayedCard> GetPlays(HandState hand)
    {
        foreach (var trick in hand.CompletedTricks)
        {
            foreach (var played in trick.PlayedCards) yield return played;
        }

        if (hand.CurrentTrick is not null)
        {
            foreach (var played in hand.CurrentTrick.PlayedCards) yield return played;
        }
    }
}
