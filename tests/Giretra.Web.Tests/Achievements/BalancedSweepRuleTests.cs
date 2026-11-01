using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;

namespace Giretra.Web.Tests.Achievements;

public sealed class BalancedSweepRuleTests
{
    private readonly BalancedSweepRule _rule = new();

    private const PlayerPosition Player = PlayerPosition.Bottom;
    private const PlayerPosition Partner = PlayerPosition.Top;
    private const PlayerPosition Opponent = PlayerPosition.Left;

    [Fact]
    public async Task Earned_WhenSweepIsSplitFourFour()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Partner, Player, Partner, Player, Partner, Player, Partner]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_RegardlessOfTrickOrder()
    {
        var context = BuildContext(
            GameMode.ColourHearts,
            [Partner, Partner, Partner, Partner, Player, Player, Player, Player]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_ForBothPartners()
    {
        var context = BuildContext(
            GameMode.NoTrumps,
            [Player, Partner, Player, Partner, Player, Partner, Player, Partner])
            with
        { PlayerPosition = Partner, PlayerTeam = Partner.GetTeam() };

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenSweepIsSplitFiveThree()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Player, Player, Player, Player, Partner, Partner, Partner]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenSweepIsSplitThreeFive()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Player, Player, Partner, Partner, Partner, Partner, Partner]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenOpponentsTakeATrick()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Partner, Player, Partner, Player, Partner, Player, Opponent],
            wasSweep: false);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenOpponentsSweep()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Opponent, PlayerPosition.Right, Opponent, PlayerPosition.Right, Opponent, PlayerPosition.Right, Opponent, PlayerPosition.Right],
            sweepingTeam: Team.Team2);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTricksAreIncomplete()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Partner, Player, Partner, Player, Partner]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThereIsNoDealResult()
    {
        var context = BuildContext(
            GameMode.AllTrumps,
            [Player, Partner, Player, Partner, Player, Partner, Player, Partner])
            with
        { DealResult = null };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    /// <summary>
    /// Builds a deal where trick i is won by <paramref name="winners"/>[i].
    /// </summary>
    private static AchievementContext BuildContext(
        GameMode gameMode,
        PlayerPosition[] winners,
        bool wasSweep = true,
        Team sweepingTeam = Team.Team1)
    {
        var dealResult = new DealResult
        {
            GameMode = gameMode,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = Team.Team1,
            Team1CardPoints = sweepingTeam == Team.Team1 ? 162 : 0,
            Team2CardPoints = sweepingTeam == Team.Team1 ? 0 : 162,
            Team1MatchPoints = sweepingTeam == Team.Team1 ? 4 : 0,
            Team2MatchPoints = sweepingTeam == Team.Team1 ? 0 : 4,
            WasSweep = wasSweep,
            SweepingTeam = wasSweep ? sweepingTeam : null
        };

        var tricks = winners
            .Select((winner, i) =>
            {
                // The winner plays the Ace; the others play distinct low cards.
                var plays = Enumerable.Range(0, 4)
                    .Select(k => (PlayerPosition)k)
                    .Select(p => new PlayedCard(
                        p,
                        new Card(p == winner ? CardRank.Ace : CardRank.Seven + (int)p, CardSuit.Clubs)))
                    .ToList();
                return new CompletedTrick(i + 1, plays, CardSuit.Clubs, winner);
            })
            .ToList();

        return new AchievementContext
        {
            Trigger = AchievementTrigger.DealEnd,
            DealResult = dealResult,
            DealNumber = 1,
            MatchState = MatchState.Create(PlayerPosition.Left),
            CompletedDeals = [dealResult],
            PlayerPosition = Player,
            PlayerTeam = Player.GetTeam(),
            PlayerId = Guid.NewGuid(),
            Tricks = tricks,
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
