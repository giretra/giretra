using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;

namespace Giretra.Web.Tests.Achievements;

public sealed class PartnerCarriedAfterCutRuleTests
{
    private readonly PartnerCarriedAfterCutRule _rule = new();

    // Team1 = Bottom + Top, Team2 = Left + Right
    private const PlayerPosition Cutter = PlayerPosition.Bottom;
    private const PlayerPosition Teammate = PlayerPosition.Top;

    [Fact]
    public async Task Earned_WhenCutterTeammateWinsSixTricksAndTeamWinsDeal()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenTeammateWinsAllEightTricks()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 8, cutter: 0, opponent: 0),
            team1MatchPoints: 8,
            team2MatchPoints: 0);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_ForTheTeammateWhoWonTheTricks()
    {
        // Same deal, but evaluated for the partner who took the tricks instead of the cutter
        var context = BuildContext(
            player: Teammate,
            cutter: Cutter,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPlayerDidNotCut()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: PlayerPosition.Left,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenCutterIsUnknown()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: null,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTeammateWinsOnlyFiveTricks()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 5, cutter: 2, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheCutterWonTheTricksHimself()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 1, cutter: 6, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTeamLostTheDeal()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 0,
            team2MatchPoints: 4);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheDealIsADraw()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 2,
            team2MatchPoints: 2);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThereIsNoDealResult()
    {
        var context = BuildContext(
            player: Cutter,
            cutter: Cutter,
            trickWinners: Winners(teammate: 6, cutter: 1, opponent: 1),
            team1MatchPoints: 4,
            team2MatchPoints: 0) with
        { DealResult = null };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    /// <summary>
    /// Builds the 8 trick winners: <paramref name="teammate"/> tricks for Top,
    /// <paramref name="cutter"/> for Bottom, <paramref name="opponent"/> for Left.
    /// </summary>
    private static List<PlayerPosition> Winners(int teammate, int cutter, int opponent)
    {
        Assert.Equal(8, teammate + cutter + opponent);

        var winners = new List<PlayerPosition>();
        winners.AddRange(Enumerable.Repeat(Teammate, teammate));
        winners.AddRange(Enumerable.Repeat(Cutter, cutter));
        winners.AddRange(Enumerable.Repeat(PlayerPosition.Left, opponent));
        return winners;
    }

    private static AchievementContext BuildContext(
        PlayerPosition player,
        PlayerPosition? cutter,
        List<PlayerPosition> trickWinners,
        int team1MatchPoints,
        int team2MatchPoints)
    {
        var dealResult = new DealResult
        {
            GameMode = GameMode.AllTrumps,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = Team.Team1,
            Team1CardPoints = 100,
            Team2CardPoints = 62,
            Team1MatchPoints = team1MatchPoints,
            Team2MatchPoints = team2MatchPoints
        };

        var tricks = trickWinners
            .Select((winner, i) => new CompletedTrick(
                i + 1,
                [
                    new PlayedCard(PlayerPosition.Bottom, new Card(CardRank.Seven, CardSuit.Spades)),
                    new PlayedCard(PlayerPosition.Left, new Card(CardRank.Eight, CardSuit.Spades)),
                    new PlayedCard(PlayerPosition.Top, new Card(CardRank.Nine, CardSuit.Spades)),
                    new PlayedCard(PlayerPosition.Right, new Card(CardRank.Ten, CardSuit.Spades))
                ],
                CardSuit.Spades,
                winner))
            .ToList();

        return new AchievementContext
        {
            Trigger = AchievementTrigger.DealEnd,
            DealResult = dealResult,
            DealNumber = 1,
            MatchState = MatchState.Create(PlayerPosition.Left),
            CompletedDeals = [dealResult],
            PlayerPosition = player,
            PlayerTeam = player.GetTeam(),
            PlayerId = Guid.NewGuid(),
            CutterPosition = cutter,
            Tricks = tricks,
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
