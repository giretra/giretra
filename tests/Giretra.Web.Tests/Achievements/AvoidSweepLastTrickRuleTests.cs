using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;

namespace Giretra.Web.Tests.Achievements;

public sealed class AvoidSweepLastTrickRuleTests
{
    private readonly AvoidSweepLastTrickRule _rule = new();

    private const PlayerPosition Player = PlayerPosition.Bottom;
    private const PlayerPosition Teammate = PlayerPosition.Top;
    private const PlayerPosition Opponent = PlayerPosition.Left;

    [Fact]
    public async Task Earned_WhenPlayerWinsOnlyTheLastTrick()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Player
        ]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTeammateWinsTheLastTrick()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Teammate
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheTeamAlsoWonAnEarlierTrick()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Teammate, Opponent, Opponent, Opponent, Opponent, Player
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThePlayerWonAnEarlierTrickToo()
    {
        var context = BuildContext(
        [
            Player, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Player
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheOnlyTrickWonIsNotTheLastOne()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Player, Opponent
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheOpponentsSweep()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThereIsNoDealResult()
    {
        var context = BuildContext(
        [
            Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Opponent, Player
        ]) with
        { DealResult = null };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    private static AchievementContext BuildContext(PlayerPosition[] trickWinners)
    {
        var dealResult = new DealResult
        {
            GameMode = GameMode.ColourHearts,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = Team.Team2,
            Team1CardPoints = 10,
            Team2CardPoints = 152,
            Team1MatchPoints = 0,
            Team2MatchPoints = 4
        };

        var tricks = trickWinners
            .Select((winner, i) => new CompletedTrick(
                i + 1,
                [
                    new PlayedCard(Player, new Card(CardRank.Seven, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Left, new Card(CardRank.Eight, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Top, new Card(CardRank.Nine, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Right, new Card(CardRank.Ten, CardSuit.Diamonds))
                ],
                CardSuit.Diamonds,
                winner))
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
