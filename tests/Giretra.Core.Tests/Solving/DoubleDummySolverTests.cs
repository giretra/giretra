using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Solving;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Solving;

public class DoubleDummySolverTests
{
    private static Card C(CardRank rank, CardSuit suit) => new(rank, suit);

    public static IEnumerable<object[]> AllModes()
        => GameModeExtensions.GetAllModes().Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Evaluate_MatchesBruteForce_OnRandomEndgames(GameMode mode)
    {
        var random = new Random(1000 + (int)mode);
        var solver = new DoubleDummySolver(mode);

        for (var i = 0; i < 40; i++)
        {
            // Random play down to the last 3 tricks, stopping at a random point within the trick
            var (hand, hands) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 20 + random.Next(4));

            var evaluation = solver.Evaluate(hand, SolverTestHelper.AsReadOnly(hands));
            var mover = hand.CurrentTrick!.CurrentPlayer!.Value;
            var validPlays = PlayValidator.GetValidPlays(Player.Create(mover, hands[mover]), hand.CurrentTrick, mode);

            Assert.Equal(mover, evaluation.Player);
            Assert.Equal(validPlays.OrderBy(c => c), evaluation.Cards.Select(e => e.Card).OrderBy(c => c));

            foreach (var card in evaluation.Cards)
            {
                var expected = SolverTestHelper.BruteForce(hand, hands, card.Card);
                Assert.Equal(expected, card.Outcome.GetScore(Team.Team1));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Evaluate_MatchesBruteForce_WithFourTricksLeft(GameMode mode)
    {
        var random = new Random(2000 + (int)mode);
        var solver = new DoubleDummySolver(mode);

        for (var i = 0; i < 3; i++)
        {
            var (hand, hands) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 16);
            var evaluation = solver.Evaluate(hand, SolverTestHelper.AsReadOnly(hands));

            foreach (var card in evaluation.Cards)
            {
                Assert.Equal(SolverTestHelper.BruteForce(hand, hands, card.Card), card.Outcome.GetScore(Team.Team1));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Evaluate_MatchesBruteForce_WhenASweepIsStillPossible(GameMode mode)
    {
        var random = new Random(2500 + (int)mode);
        var solver = new DoubleDummySolver(mode);

        for (var i = 0; i < 10; i++)
        {
            var (hand, hands) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 20,
                accept: h => h.Team1TricksWon == 0 || h.Team2TricksWon == 0);
            var evaluation = solver.Evaluate(hand, SolverTestHelper.AsReadOnly(hands));

            foreach (var card in evaluation.Cards)
            {
                Assert.Equal(SolverTestHelper.BruteForce(hand, hands, card.Card), card.Outcome.GetScore(Team.Team1));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void Evaluate_SolvesFullDeals_ConsistentlyWithSolve(GameMode mode)
    {
        var random = new Random(3000 + (int)mode);

        for (var i = 0; i < 5; i++)
        {
            var (hand, hands) = SolverTestHelper.PlayRandomly(mode, random, cardsToPlay: 0);
            var readOnlyHands = SolverTestHelper.AsReadOnly(hands);

            var evaluation = new DoubleDummySolver(mode).Evaluate(hand, readOnlyHands);
            var outcome = new DoubleDummySolver(mode).Solve(hand, readOnlyHands);

            Assert.Equal(8, evaluation.Cards.Count);
            Assert.Equal(outcome, evaluation.Best.Outcome);
            Assert.Equal(mode.GetTotalPoints(), outcome.Team1CardPoints + outcome.Team2CardPoints);
            Assert.True(evaluation.Best.IsOptimal);
            Assert.Equal(0, evaluation.Best.CardPointLoss);
            Assert.All(evaluation.Cards, c => Assert.True(c.Score <= evaluation.Best.Score));
        }
    }

    [Fact]
    public void Evaluate_GivesUpAWorthlessTrickToWinTheLastOne()
    {
        // NoTrumps, last two tricks, Bottom leads.
        // A♠ first wins 11 points, but then 8♦ loses the last trick (and its 10 bonus) to Right's 9♦.
        // 8♦ first loses a worthless trick, then the Ace wins the last trick: 11 + 10.
        var hand = SolverTestHelper.HandWithTricksWonBy(GameMode.NoTrumps, PlayerPosition.Bottom, tricks: 6);
        var hands = new Dictionary<PlayerPosition, IReadOnlyList<Card>>
        {
            [PlayerPosition.Bottom] = [C(CardRank.Ace, CardSuit.Spades), C(CardRank.Eight, CardSuit.Diamonds)],
            [PlayerPosition.Left] = [C(CardRank.Seven, CardSuit.Spades), C(CardRank.Seven, CardSuit.Clubs)],
            [PlayerPosition.Top] = [C(CardRank.Eight, CardSuit.Spades), C(CardRank.Eight, CardSuit.Clubs)],
            [PlayerPosition.Right] = [C(CardRank.Nine, CardSuit.Spades), C(CardRank.Nine, CardSuit.Diamonds)]
        };

        var evaluation = new DoubleDummySolver(GameMode.NoTrumps).Evaluate(hand, hands);

        var ace = evaluation.GetCard(C(CardRank.Ace, CardSuit.Spades));
        var eight = evaluation.GetCard(C(CardRank.Eight, CardSuit.Diamonds));

        Assert.Equal(eight, evaluation.Best);
        Assert.True(eight.IsOptimal);
        Assert.False(ace.IsOptimal);
        Assert.Equal(10, ace.CardPointLoss);
        Assert.Equal(hand.Team1CardPoints + 21, eight.Outcome.Team1CardPoints);
        Assert.Null(eight.Outcome.SweepingTeam);
    }

    [Fact]
    public void Evaluate_DetectsASweep()
    {
        var hand = SolverTestHelper.HandWithTricksWonBy(GameMode.NoTrumps, PlayerPosition.Bottom, tricks: 7);
        var hands = new Dictionary<PlayerPosition, IReadOnlyList<Card>>
        {
            [PlayerPosition.Bottom] = [C(CardRank.Ace, CardSuit.Spades)],
            [PlayerPosition.Left] = [C(CardRank.Seven, CardSuit.Spades)],
            [PlayerPosition.Top] = [C(CardRank.Eight, CardSuit.Spades)],
            [PlayerPosition.Right] = [C(CardRank.Nine, CardSuit.Spades)]
        };

        var evaluation = new DoubleDummySolver(GameMode.NoTrumps).Evaluate(hand, hands);

        Assert.Equal(Team.Team1, evaluation.Best.Outcome.SweepingTeam);
        Assert.Equal(hand.Team1CardPoints + 11 + 10, evaluation.Best.Outcome.Team1CardPoints);
        Assert.Equal(evaluation.Best.Outcome.Team1CardPoints + DoubleDummySolver.SweepBonus, evaluation.Best.Score);
    }

    [Fact]
    public void Evaluate_RejectsInconsistentInput()
    {
        var solver = new DoubleDummySolver(GameMode.NoTrumps);
        var hand = HandState.Create(GameMode.NoTrumps, PlayerPosition.Bottom);

        var duplicated = new Dictionary<PlayerPosition, IReadOnlyList<Card>>
        {
            [PlayerPosition.Bottom] = [C(CardRank.Ace, CardSuit.Spades)],
            [PlayerPosition.Left] = [C(CardRank.Ace, CardSuit.Spades)],
            [PlayerPosition.Top] = [C(CardRank.Eight, CardSuit.Spades)],
            [PlayerPosition.Right] = [C(CardRank.Nine, CardSuit.Spades)]
        };
        Assert.Throws<ArgumentException>(() => solver.Evaluate(hand, duplicated));

        var uneven = new Dictionary<PlayerPosition, IReadOnlyList<Card>>
        {
            [PlayerPosition.Bottom] = [C(CardRank.Ace, CardSuit.Spades)],
            [PlayerPosition.Left] = [C(CardRank.Seven, CardSuit.Spades), C(CardRank.Seven, CardSuit.Hearts)],
            [PlayerPosition.Top] = [C(CardRank.Eight, CardSuit.Spades)],
            [PlayerPosition.Right] = [C(CardRank.Nine, CardSuit.Spades)]
        };
        Assert.Throws<ArgumentException>(() => solver.Evaluate(hand, uneven));

        Assert.Throws<ArgumentException>(() =>
            new DoubleDummySolver(GameMode.AllTrumps).Evaluate(hand, uneven));
    }
}
