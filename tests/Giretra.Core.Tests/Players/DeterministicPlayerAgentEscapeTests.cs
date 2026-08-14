using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Players;

/// <summary>
/// Tests for the colour-mode "escape" plays: banking a vulnerable trump 10/A
/// (or a side-suit 10-pointer) on a trick the team already holds, instead of
/// letting opponents capture it later.
/// </summary>
public class DeterministicPlayerAgentEscapeTests
{
    private static Card C(CardRank rank, CardSuit suit) => new(rank, suit);

    /// <summary>
    /// Sets up an agent at Bottom, replays the given cards into a trick led by
    /// <paramref name="leader"/>, then asks the agent for its play.
    /// </summary>
    private static async Task<Card> ChooseCardAsync(
        GameMode mode,
        PlayerPosition leader,
        Card[] playedCards,
        Card[] hand,
        Card[] validPlays)
    {
        var agent = new DeterministicPlayerAgent(PlayerPosition.Bottom);
        var matchState = MatchState.Create(PlayerPosition.Right);
        await agent.OnDealStartedAsync(matchState);

        var handState = HandState.Create(mode, leader);
        var player = leader;
        foreach (var card in playedCards)
        {
            handState = handState.PlayCard(card);
            await agent.OnCardPlayedAsync(player, card, handState, matchState);
            player = player.Next();
        }

        return await agent.ChooseCardAsync(hand, handState, matchState, validPlays);
    }

    [Fact]
    public async Task FourthSeat_DoesNotDonateTenToOpponentRuff()
    {
        // Colour Hearts. Right (opponent) ruffed the diamond lead and is winning.
        // Bottom follows suit holding D10 + D9: dumping the 10 would donate points.
        var played = new[] { C(CardRank.Seven, CardSuit.Diamonds), C(CardRank.Eight, CardSuit.Diamonds), C(CardRank.Queen, CardSuit.Hearts) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Diamonds), C(CardRank.Nine, CardSuit.Diamonds), C(CardRank.Seven, CardSuit.Clubs) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Diamonds), C(CardRank.Nine, CardSuit.Diamonds) };

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Nine, CardSuit.Diamonds), chosen);
    }

    [Fact]
    public async Task FourthSeat_EscapesVulnerableTenOntoTeammatesTrick()
    {
        // NoTrumps. Top (teammate) is winning with the spade K. Bottom holds
        // S10 (catchable by the unseen ace) — bank it now, it wins the trick too.
        var played = new[] { C(CardRank.Queen, CardSuit.Spades), C(CardRank.King, CardSuit.Spades), C(CardRank.Seven, CardSuit.Spades) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Spades), C(CardRank.Nine, CardSuit.Spades), C(CardRank.Seven, CardSuit.Clubs) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Spades), C(CardRank.Nine, CardSuit.Spades) };

        var chosen = await ChooseCardAsync(GameMode.NoTrumps, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Ten, CardSuit.Spades), chosen);
    }

    [Fact]
    public async Task Undertrump_EscapesTrumpTenUnderPartnersMasterJack()
    {
        // Colour Hearts. Top (teammate) ruffed the spade lead with the trump J
        // (master). Bottom is void in spades, must trump, cannot overtrump:
        // the classic escape — throw the trump 10 under partner's J, not the 7.
        var played = new[] { C(CardRank.Ace, CardSuit.Spades), C(CardRank.Jack, CardSuit.Hearts), C(CardRank.Eight, CardSuit.Spades) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Diamonds) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts) };

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Ten, CardSuit.Hearts), chosen);
    }

    [Fact]
    public async Task Ruff_UsesVulnerableTrumpTenWhenNoOpponentCanOvertrump()
    {
        // Colour Hearts. Left (opponent) leads clubs and is winning; Bottom is
        // void, must ruff, and is last to play: ruffing with the doomed trump 10
        // banks it instead of spending the 7 and losing the 10 later.
        var played = new[] { C(CardRank.King, CardSuit.Clubs), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Eight, CardSuit.Clubs) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Spades) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts) };

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Ten, CardSuit.Hearts), chosen);
    }

    [Fact]
    public async Task Ruff_StaysLowWhenAnOpponentBehindMayOvertrump()
    {
        // Same ruff, but from 2nd seat with an opponent still to play who may
        // hold a higher trump: escaping the 10 would hand it straight to them.
        var played = new[] { C(CardRank.King, CardSuit.Clubs) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Spades) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts) };

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Right, played, hand, valid);

        Assert.Equal(C(CardRank.Seven, CardSuit.Hearts), chosen);
    }

    [Fact]
    public async Task TeammateWinningNonTrump_TrumpsOwnTrickToEscapeDoomedTen()
    {
        // Colour Hearts. Top (teammate) is winning the diamond trick with the
        // master ace. Bottom is void and may discard — but ruffing with the
        // doomed trump 10 rescues 10 points the opponents would capture later.
        var played = new[] { C(CardRank.King, CardSuit.Diamonds), C(CardRank.Ace, CardSuit.Diamonds), C(CardRank.Eight, CardSuit.Diamonds) };
        var hand = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Clubs) };
        var valid = new[] { C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Clubs) };

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Ten, CardSuit.Hearts), chosen);
    }

    [Fact]
    public async Task ProtectedTrumpTen_IsNotEscaped()
    {
        // As in the safe-ruff test, but the 10 is guarded: enough low trumps to
        // feed under every remaining pull, so it should be kept, not escaped.
        var played = new[] { C(CardRank.King, CardSuit.Clubs), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Eight, CardSuit.Clubs) };
        var hand = new[]
        {
            C(CardRank.Ten, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Hearts), C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Eight, CardSuit.Hearts),
        };
        var valid = hand;

        var chosen = await ChooseCardAsync(GameMode.ColourHearts, PlayerPosition.Left, played, hand, valid);

        Assert.Equal(C(CardRank.Seven, CardSuit.Hearts), chosen);
    }
}
