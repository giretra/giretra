using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;
using Giretra.Web.Players;

namespace Giretra.Web.Tests.Achievements;

public sealed class ThreeDoublesMatchWinRuleTests : IAsyncLifetime
{
    private readonly ThreeDoublesMatchWinRule _rule = new();

    /// <summary>A genuinely completed match, played to a target of 1 point.</summary>
    private MatchState _completedMatch = null!;

    /// <summary>Who performed a recorded action, relative to the player being evaluated.</summary>
    private enum Actor
    {
        Self,
        Partner,
        Opponent
    }

    public async Task InitializeAsync()
    {
        var manager = new GameManager(
            new RandomPlayerAgent(PlayerPosition.Bottom, seed: 1),
            new RandomPlayerAgent(PlayerPosition.Left, seed: 2),
            new RandomPlayerAgent(PlayerPosition.Top, seed: 3),
            new RandomPlayerAgent(PlayerPosition.Right, seed: 4),
            firstDealer: PlayerPosition.Bottom,
            deckProvider: () => Deck.CreateShuffled(new Random(42)),
            targetScore: 1);

        _completedMatch = await manager.PlayMatchAsync();

        Assert.True(_completedMatch.IsComplete);
        Assert.NotNull(_completedMatch.Winner);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Earned_WhenTeamWinsAndPlayerDoubledInThreeDeals()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)]
        ]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenChallengesAreAMixOfDoubleRedoubleAndReRedouble()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Redouble)],
            [(Actor.Self, RecordedActionType.ReRedouble)]
        ]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenPlayerChallengedInMoreThanThreeDeals()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Opponent, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Redouble)],
            [(Actor.Self, RecordedActionType.Double)]
        ]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPlayerChallengedInOnlyTwoDeals()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Redouble)],
            [(Actor.Self, RecordedActionType.Accept)]
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThreeChallengesHappenInsideTwoDeals()
    {
        // Several challenges in one deal count once, not once each
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double), (Actor.Self, RecordedActionType.Redouble)],
            [(Actor.Self, RecordedActionType.Double)]
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheChallengesCameFromThePartner()
    {
        var context = BuildContext(
        [
            [(Actor.Partner, RecordedActionType.Double)],
            [(Actor.Partner, RecordedActionType.Double)],
            [(Actor.Partner, RecordedActionType.Double)]
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPlayerOnlyAnnouncedOrAccepted()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Announce)],
            [(Actor.Self, RecordedActionType.Accept)],
            [(Actor.Self, RecordedActionType.Announce)]
        ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheTeamLostTheMatch()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)]
        ],
        teamWonMatch: false);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheMatchIsNotComplete()
    {
        var context = BuildContext(
        [
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)],
            [(Actor.Self, RecordedActionType.Double)]
        ],
        matchComplete: false);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    /// <summary>
    /// Builds a match-end context whose negotiations are the given per-deal actions.
    /// The evaluated player is seated on the winning team unless
    /// <paramref name="teamWonMatch"/> is false.
    /// </summary>
    private AchievementContext BuildContext(
        List<List<(Actor Actor, RecordedActionType Type)>> deals,
        bool teamWonMatch = true,
        bool matchComplete = true)
    {
        var winningSeat = _completedMatch.Winner == Team.Team1
            ? PlayerPosition.Bottom
            : PlayerPosition.Left;

        // Adjacent seats always belong to opposing teams
        var player = teamWonMatch ? winningSeat : winningSeat.Next();

        var negotiations = deals
            .Select((actions, i) => new DealNegotiation(
                i + 1,
                actions
                    .Select((a, order) => new RecordedAction
                    {
                        ActionOrder = order,
                        ActionType = a.Type,
                        PlayerPosition = a.Actor switch
                        {
                            Actor.Self => player,
                            Actor.Partner => player.Teammate(),
                            _ => player.Next()
                        }
                    })
                    .ToList()))
            .ToList();

        return new AchievementContext
        {
            Trigger = AchievementTrigger.MatchEnd,
            MatchState = matchComplete ? _completedMatch : MatchState.Create(PlayerPosition.Bottom),
            CompletedDeals = matchComplete ? _completedMatch.CompletedDeals : [],
            PlayerPosition = player,
            PlayerTeam = player.GetTeam(),
            PlayerId = Guid.NewGuid(),
            MatchNegotiations = negotiations,
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
