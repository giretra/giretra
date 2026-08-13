using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Players;

/// <summary>
/// Verifies the cut projection matches what the engine actually deals, so an agent can rely
/// on it when choosing a cut.
/// </summary>
public class CutPlannerTests
{
    [Fact]
    public void CandidateCutPositions_CoverTheLegalRange()
    {
        Assert.Equal(21, CutPlanner.CandidateCutPositions.Count);
        Assert.Equal(6, CutPlanner.CandidateCutPositions[0]);
        Assert.Equal(26, CutPlanner.CandidateCutPositions[^1]);
    }

    /// <summary>
    /// Cutting from the bottom is never needed: it produces the same deck as cutting
    /// 32 - position from the top, which the candidate list already covers.
    /// </summary>
    [Fact]
    public void CuttingFromTheBottom_ProducesADeckCuttingFromTheTopAlsoProduces()
    {
        var deck = Deck.CreateShuffled(new Random(7));

        foreach (var position in CutPlanner.CandidateCutPositions)
        {
            var fromBottom = deck.Cut(position, fromTop: false);
            var fromTop = deck.Cut(CutPlanner.DeckSize - position, fromTop: true);

            Assert.Equal(fromTop.Cards, fromBottom.Cards);
        }
    }

    [Theory]
    [InlineData(PlayerPosition.Bottom)]
    [InlineData(PlayerPosition.Left)]
    [InlineData(PlayerPosition.Top)]
    [InlineData(PlayerPosition.Right)]
    public void SeatIndex_FollowsTheDealingOrder(PlayerPosition dealer)
    {
        Assert.Equal(0, CutPlanner.SeatIndex(dealer, dealer.Next()));
        Assert.Equal(1, CutPlanner.SeatIndex(dealer, dealer.Teammate()));
        Assert.Equal(2, CutPlanner.SeatIndex(dealer, dealer.Previous()));
        Assert.Equal(3, CutPlanner.SeatIndex(dealer, dealer));
    }

    /// <summary>
    /// The projection has to agree with the engine for every dealer and every legal cut, both
    /// for the five cards negotiation happens on and for the full eight-card hand.
    /// </summary>
    [Theory]
    [InlineData(PlayerPosition.Bottom)]
    [InlineData(PlayerPosition.Left)]
    [InlineData(PlayerPosition.Top)]
    [InlineData(PlayerPosition.Right)]
    public void ProjectHand_MatchesTheCardsTheEngineDeals(PlayerPosition dealer)
    {
        var deck = Deck.CreateShuffled(new Random(1234));

        foreach (var position in CutPlanner.CandidateCutPositions)
        {
            var deal = DealState.Create(dealer, deck)
                .CutDeck(position, fromTop: true)
                .PerformInitialDistribution();

            foreach (var player in Enum.GetValues<PlayerPosition>())
            {
                var projected = CutPlanner.ProjectHand(deck, dealer, player, position);

                Assert.Equal(CutPlanner.FullHandSize, projected.Count);
                Assert.Equal(
                    deal.GetPlayer(player).Hand.ToHashSet(),
                    projected.Take(CutPlanner.NegotiationHandSize).ToHashSet());
            }

            var played = CompleteNegotiation(deal).PerformFinalDistribution();

            foreach (var player in Enum.GetValues<PlayerPosition>())
            {
                var projected = CutPlanner.ProjectHand(deck, dealer, player, position);

                Assert.Equal(
                    played.GetPlayer(player).Hand.ToHashSet(),
                    projected.ToHashSet());
            }
        }
    }

    [Fact]
    public void ProjectHand_RejectsAnIllegalCut()
    {
        var deck = Deck.CreateShuffled(new Random(3));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CutPlanner.ProjectHand(deck, PlayerPosition.Bottom, PlayerPosition.Top, 5));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CutPlanner.ProjectHand(deck, PlayerPosition.Bottom, PlayerPosition.Top, 27));
    }

    [Fact]
    public void ProjectHand_RejectsAPartialDeck()
    {
        var partial = Deck.FromCards(Deck.CreateStandard().Cards.Take(20));

        Assert.Throws<ArgumentException>(
            () => CutPlanner.ProjectHand(partial, PlayerPosition.Bottom, PlayerPosition.Top, 16));
    }

    /// <summary>
    /// Announces a plain Colour and accepts it round the table, which is the shortest path to a
    /// completed negotiation.
    /// </summary>
    private static DealState CompleteNegotiation(DealState deal)
    {
        var announcement = new AnnouncementAction(deal.Dealer.Next(), GameMode.ColourHearts);
        deal = deal.ApplyNegotiationAction(announcement);

        while (!deal.Negotiation!.IsComplete)
        {
            var accept = NegotiationEngine.GetValidActions(deal.Negotiation)
                .OfType<AcceptAction>()
                .First();

            deal = deal.ApplyNegotiationAction(accept);
        }

        return deal;
    }
}
