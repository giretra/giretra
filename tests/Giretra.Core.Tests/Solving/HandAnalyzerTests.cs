using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Solving;

namespace Giretra.Core.Tests.Solving;

public class HandAnalyzerTests
{
    public static IEnumerable<object[]> AllModes()
        => GameModeExtensions.GetAllModes().Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Analyze_LossesExplainTheGapBetweenParAndTheActualResult(GameMode mode)
    {
        var random = new Random(4000 + (int)mode);
        var (hand, _) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 32);

        var analysis = HandAnalyzer.Analyze(hand);

        Assert.Equal(32, analysis.Plays.Count);
        Assert.Equal(hand.Team1CardPoints, analysis.Final.Team1CardPoints);
        Assert.Equal(hand.Team2CardPoints, analysis.Final.Team2CardPoints);
        Assert.Equal(hand.SweepingTeam, analysis.Final.SweepingTeam);

        // Every mistake moves the solver score away from the mistaken team, and nothing else moves it
        var score = analysis.Par.GetScore(Team.Team1);
        foreach (var play in analysis.Plays)
        {
            var loss = play.Evaluation.Best.Score - play.Evaluation.GetCard(play.Card).Score;
            Assert.True(loss >= 0);
            Assert.Equal(loss == 0, play.IsOptimal);
            score += play.Player.GetTeam() == Team.Team1 ? -loss : loss;
        }

        Assert.Equal(analysis.Final.GetScore(Team.Team1), score);
    }

    [Fact]
    public void Analyze_PerfectPlayHasNoMistakes()
    {
        const GameMode mode = GameMode.ColourHearts;
        var (hand, hands) = SolverTestHelper.PlayRandomly(mode, new Random(7), cardsToPlay: 0);
        var startingHands = SolverTestHelper.AsReadOnly(hands);
        var solver = new DoubleDummySolver(mode);

        while (!hand.IsComplete)
        {
            var best = solver.Evaluate(hand, SolverTestHelper.AsReadOnly(hands)).Best.Card;
            hands[hand.CurrentTrick!.CurrentPlayer!.Value].Remove(best);
            hand = hand.PlayCard(best);
        }

        var analysis = HandAnalyzer.Analyze(hand, new SolverScoring(Team.Team1), solver);

        Assert.Empty(analysis.Mistakes);
        Assert.Equal(analysis.Par, analysis.Final);
        foreach (var player in Enum.GetValues<PlayerPosition>())
        {
            var summary = analysis.GetSummary(player);
            Assert.Equal(8, summary.Plays);
            Assert.Equal(0, summary.Mistakes);
            Assert.Equal(0, summary.CardPointLoss);
            Assert.Equal(0, summary.MatchPointLoss);
        }

        // Same starting hands, explicit overload
        var explicitAnalysis = HandAnalyzer.Analyze(hand, startingHands, solver: solver);
        Assert.Equal(analysis.Par, explicitAnalysis.Par);
        Assert.All(explicitAnalysis.Plays, p => Assert.Null(p.MatchPointLoss));
    }

    [Fact]
    public void Analyze_SupportsAHandInProgress()
    {
        const GameMode mode = GameMode.AllTrumps;
        var random = new Random(11);
        var (start, startingHands) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 0);

        // Same deal, 14 cards in
        var (hand, hands) = SolverTestHelper.PlayRandomly(mode, new Random(11), cardsToPlay: 14);

        var analysis = HandAnalyzer.Analyze(hand, SolverTestHelper.AsReadOnly(startingHands));

        Assert.Equal(start.CurrentTrick!.Leader, analysis.Plays[0].Player);
        Assert.Equal(14, analysis.Plays.Count);
        Assert.Equal(new DoubleDummySolver(mode).Solve(hand, SolverTestHelper.AsReadOnly(hands)), analysis.Final);
    }

    [Fact]
    public void Analyze_RequiresStartingHandsForAnIncompleteHand()
    {
        var (hand, _) = SolverTestHelper.PlayRandomly(GameMode.NoTrumps, new Random(3), cardsToPlay: 10);

        Assert.Throws<ArgumentException>(() => HandAnalyzer.Analyze(hand));
    }

    [Fact]
    public void GetMatchPointLoss_UsesTheScoringRules()
    {
        // Colour Hearts announced by Team1, which holds 70 card points with two tricks left (no trump left).
        var hand = SolverTestHelper.HandWithTricksWonBy(GameMode.ColourHearts, PlayerPosition.Bottom, tricks: 5);
        Assert.Equal(70, hand.Team1CardPoints);

        Card C(CardRank rank, CardSuit suit) => new(rank, suit);
        var hands = new Dictionary<PlayerPosition, IReadOnlyList<Card>>
        {
            [PlayerPosition.Bottom] = [C(CardRank.Ace, CardSuit.Spades), C(CardRank.Eight, CardSuit.Diamonds)],
            [PlayerPosition.Left] = [C(CardRank.Seven, CardSuit.Spades), C(CardRank.Seven, CardSuit.Clubs)],
            [PlayerPosition.Top] = [C(CardRank.Eight, CardSuit.Spades), C(CardRank.Eight, CardSuit.Clubs)],
            [PlayerPosition.Right] = [C(CardRank.Nine, CardSuit.Spades), C(CardRank.Nine, CardSuit.Diamonds)]
        };

        var evaluation = new DoubleDummySolver(GameMode.ColourHearts).Evaluate(hand, hands);
        var scoring = new SolverScoring(Team.Team1);

        // 8♦ first: 70 + 21 = 91 → Team1 makes its 82 and scores 16.
        // A♠ first: 70 + 11 = 81 → an 81-81 tie, nobody scores.
        Assert.Equal(16, scoring.GetMatchPointSwing(GameMode.ColourHearts, evaluation.Best.Outcome, Team.Team1));
        Assert.Equal(-16, scoring.GetMatchPointSwing(GameMode.ColourHearts, evaluation.Best.Outcome, Team.Team2));
        Assert.Equal(0, evaluation.GetMatchPointLoss(C(CardRank.Eight, CardSuit.Diamonds), scoring));
        Assert.Equal(16, evaluation.GetMatchPointLoss(C(CardRank.Ace, CardSuit.Spades), scoring));
    }
}
