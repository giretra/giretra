using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Players;

/// <summary>
/// Covers how <see cref="CuttingPlayerAgent"/> cuts once it knows the deck order.
/// </summary>
public class CuttingPlayerAgentTests
{
    private static readonly CardRank[] StrongRanks =
        [CardRank.Jack, CardRank.Nine, CardRank.Ace, CardRank.Ten];

    [Fact]
    public void RankCuts_RanksEveryReachableCutBestFirst()
    {
        var agent = new CuttingPlayerAgent(PlayerPosition.Right);
        var deck = Deck.CreateShuffled(new Random(5));

        var ranking = agent.RankCuts(deck, PlayerPosition.Bottom, MatchState.Create(PlayerPosition.Bottom));

        Assert.Equal(CutPlanner.CandidateCutPositions.Count, ranking.Count);
        Assert.Equal(CutPlanner.CandidateCutPositions.Order(), ranking.Select(e => e.Position).Order());
        Assert.Equal(ranking.Select(e => e.Score).OrderByDescending(score => score), ranking.Select(e => e.Score));
    }

    [Fact]
    public void RankCuts_IsDeterministic()
    {
        var deck = Deck.CreateShuffled(new Random(17));
        var matchState = MatchState.Create(PlayerPosition.Bottom);

        var first = new CuttingPlayerAgent(PlayerPosition.Right)
            .RankCuts(deck, PlayerPosition.Bottom, matchState);
        var second = new CuttingPlayerAgent(PlayerPosition.Right)
            .RankCuts(deck, PlayerPosition.Bottom, matchState);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// With every Jack, Nine, Ace and Ten sitting on the slots the cutter's team receives at one
    /// particular cut, that cut is the one to find.
    /// </summary>
    [Fact]
    public void RankCuts_FindsTheCutThatDealsOurTeamTheStrongCards()
    {
        const int targetCut = 13;
        var dealer = PlayerPosition.Bottom;
        var cutter = dealer.Previous();

        var deck = StackDeck(dealer, cutter, targetCut);
        var agent = new CuttingPlayerAgent(cutter);

        var ranking = agent.RankCuts(deck, dealer, MatchState.Create(dealer));

        Assert.Equal(targetCut, ranking[0].Position);
    }

    /// <summary>
    /// The choice has to depend on the deck: a search that always answers the same thing is no
    /// better than the fixed cut it replaced.
    /// </summary>
    [Fact]
    public void RankCuts_PicksDifferentCutsForDifferentDecks()
    {
        var agent = new CuttingPlayerAgent(PlayerPosition.Right);
        var matchState = MatchState.Create(PlayerPosition.Bottom);

        var chosen = Enumerable.Range(1, 20)
            .Select(seed => agent.RankCuts(
                Deck.CreateShuffled(new Random(seed)), PlayerPosition.Bottom, matchState)[0].Position)
            .ToHashSet();

        Assert.True(chosen.Count > 1, $"Expected varied cuts, got {string.Join(", ", chosen)}.");
    }

    [Fact]
    public async Task TheFirstCutOfAMatch_FallsBackToAFixedPosition()
    {
        var agent = new CuttingPlayerAgent(PlayerPosition.Right);
        var matchState = MatchState.Create(PlayerPosition.Bottom)
            .StartDeal(Deck.CreateShuffled(new Random(2)));

        var (position, fromTop) = await agent.ChooseCutAsync(CutPlanner.DeckSize, matchState);

        Assert.Equal(16, position);
        Assert.True(fromTop);
        Assert.Equal(0, agent.TrackedCuts);
    }

    /// <summary>
    /// Plays real matches and checks the tracked deck order against reality: every cut the agents
    /// made from a tracked deck must have been followed by exactly the cards they projected.
    /// Matches are chained the way hosts run them — each dealt from the deck the previous one
    /// left on the table — so the tracked order stays valid across match boundaries and only
    /// the very first deal of the first match is cut blind.
    /// </summary>
    [Fact]
    public async Task OverChainedMatches_EveryTrackedCutProjectsTheHandThatIsDealt()
    {
        var agents = new[]
        {
            new CuttingPlayerAgent(PlayerPosition.Bottom),
            new CuttingPlayerAgent(PlayerPosition.Left),
            new CuttingPlayerAgent(PlayerPosition.Top),
            new CuttingPlayerAgent(PlayerPosition.Right),
        };

        var deals = 0;
        Deck? tableDeck = null;

        for (var match = 1; match <= 5; match++)
        {
            var deck = tableDeck;
            var gameManager = new GameManager(
                agents[0],
                agents[1],
                agents[2],
                agents[3],
                PlayerPosition.Bottom,
                deck is null ? () => Deck.CreateShuffled(new Random(1)) : () => deck);

            var matchState = await gameManager.PlayMatchAsync();

            Assert.True(matchState.IsComplete);
            deals += matchState.CompletedDeals.Count;
            tableDeck = gameManager.FinalDeck;
        }

        Assert.True(deals > 4, $"Expected several deals to cut in, got {deals}.");

        foreach (var agent in agents)
        {
            Assert.True(agent.TrackedCuts > 0, $"{agent.Position} never cut from a tracked deck.");
            Assert.Equal(0, agent.CutProjectionMismatches);
        }

        // Only the very first deal of the first match was cut without a tracked deck.
        Assert.Equal(deals - 1, agents.Sum(agent => agent.TrackedCuts));
    }

    /// <summary>
    /// If a host does reshuffle between matches, the order remembered from the previous match is
    /// wrong exactly once: the first cut of the new match projects a hand that is not dealt, and
    /// the tracker is dropped rather than trusted any further.
    /// </summary>
    [Fact]
    public async Task WhenTheNextMatchIsReshuffled_TheStaleOrderIsCaughtAtTheFirstCut()
    {
        var agents = new[]
        {
            new CuttingPlayerAgent(PlayerPosition.Bottom),
            new CuttingPlayerAgent(PlayerPosition.Left),
            new CuttingPlayerAgent(PlayerPosition.Top),
            new CuttingPlayerAgent(PlayerPosition.Right),
        };

        for (var seed = 1; seed <= 2; seed++)
        {
            var deckRandom = new Random(seed);
            var gameManager = new GameManager(
                agents[0],
                agents[1],
                agents[2],
                agents[3],
                PlayerPosition.Bottom,
                () => Deck.CreateShuffled(deckRandom));

            await gameManager.PlayMatchAsync();
        }

        Assert.Equal(1, agents.Sum(agent => agent.CutProjectionMismatches));
    }

    /// <summary>
    /// The cut is the only thing this agent adds, so head to head against the agent it delegates
    /// to, the cut alone has to carry it. Both sides are deterministic and the decks are seeded,
    /// so the outcome is fixed. Measured at 76.8% over 1000 matches.
    /// </summary>
    [Fact]
    public async Task AgainstTheAgentItDelegatesTo_TheCutAloneWinsTheMajorityOfMatches()
    {
        const int matches = 200;
        var wins = 0;

        for (var seed = 1; seed <= matches; seed++)
        {
            var deckRandom = new Random(seed);

            // Alternate which team cuts deliberately, so seating is not a confound.
            var cuttingIsTeam1 = seed % 2 == 0;

            static IPlayerAgent Make(PlayerPosition position, bool cutting)
                => cutting ? new CuttingPlayerAgent(position) : new DeterministicPlayerAgent(position);

            var gameManager = new GameManager(
                Make(PlayerPosition.Bottom, cuttingIsTeam1),
                Make(PlayerPosition.Left, !cuttingIsTeam1),
                Make(PlayerPosition.Top, cuttingIsTeam1),
                Make(PlayerPosition.Right, !cuttingIsTeam1),
                PlayerPosition.Bottom,
                () => Deck.CreateShuffled(deckRandom));

            var matchState = await gameManager.PlayMatchAsync();

            if (matchState.Winner == (cuttingIsTeam1 ? Team.Team1 : Team.Team2))
                wins++;
        }

        Assert.True(wins > matches * 0.6, $"Cutting agent won only {wins} of {matches} matches.");
    }

    /// <summary>
    /// Builds a deck in which the Jacks, Nines, Aces and Tens all land on the slots dealt to the
    /// cutter's team when the deck is cut at <paramref name="cutPosition"/>.
    /// </summary>
    private static Deck StackDeck(PlayerPosition dealer, PlayerPosition cutter, int cutPosition)
    {
        var ourSlots = new[] { cutter, cutter.Teammate() }
            .SelectMany(player => SlotsOf(dealer, player))
            .ToHashSet();

        var cards = new Card?[CutPlanner.DeckSize];
        var strong = new Queue<Card>(Deck.CreateStandard().Cards.Where(c => StrongRanks.Contains(c.Rank)));
        var weak = new Queue<Card>(Deck.CreateStandard().Cards.Where(c => !StrongRanks.Contains(c.Rank)));

        for (var slot = 0; slot < CutPlanner.DeckSize; slot++)
        {
            // Slot s of the cut deck comes from index (cutPosition + s) % 32 of the uncut deck.
            var index = (cutPosition + slot) % CutPlanner.DeckSize;
            cards[index] = ourSlots.Contains(slot) ? strong.Dequeue() : weak.Dequeue();
        }

        return Deck.FromCards(cards.Select(card => card!.Value));
    }

    private static IEnumerable<int> SlotsOf(PlayerPosition dealer, PlayerPosition player)
    {
        var seat = CutPlanner.SeatIndex(dealer, player);

        yield return 3 * seat;
        yield return 3 * seat + 1;
        yield return 3 * seat + 2;
        yield return 12 + 2 * seat;
        yield return 13 + 2 * seat;
        yield return 20 + 3 * seat;
        yield return 21 + 3 * seat;
        yield return 22 + 3 * seat;
    }
}
