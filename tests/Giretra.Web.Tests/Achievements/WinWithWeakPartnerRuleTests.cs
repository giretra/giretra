using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;

namespace Giretra.Web.Tests.Achievements;

public sealed class WinWithWeakPartnerRuleTests : IAsyncLifetime
{
    private readonly WinWithWeakPartnerRule _rule = new();

    /// <summary>A genuinely completed match, played to a target of 1 point.</summary>
    private MatchState _completedMatch = null!;

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
    public async Task Earned_WhenTeamWinsWithPartnerAt1100AndOpponentsAt1400()
    {
        var context = BuildContext(partner: 1100, opponents: [1400, 1400]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenPartnerIsWellBelowThreshold()
    {
        var context = BuildContext(self: 1800, partner: 863, opponents: [1546, 1425]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPartnerIsJustAboveThreshold()
    {
        var context = BuildContext(partner: 1101, opponents: [1500, 1500]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenOnlyThePlayerIsLowRated()
    {
        // The low rating must be the partner's, not the evaluated player's
        var context = BuildContext(self: 900, partner: 1300, opponents: [1500, 1500]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenAnOpponentIsBelow1400()
    {
        var context = BuildContext(partner: 1000, opponents: [1399, 1500]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheTeamLostTheMatch()
    {
        var context = BuildContext(partner: 1000, opponents: [1500, 1500], teamWonMatch: false);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTheMatchIsNotComplete()
    {
        var context = BuildContext(partner: 1000, opponents: [1500, 1500], matchComplete: false);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenRatingsAreUnknown()
    {
        var context = BuildContext(partner: 1000, opponents: [1500, 1500]) with
        {
            RatingsBefore = new Dictionary<PlayerPosition, int>()
        };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenOnlyThePartnerRatingIsMissing()
    {
        var full = BuildContext(partner: 1000, opponents: [1500, 1500]);
        var ratings = new Dictionary<PlayerPosition, int>(full.RatingsBefore);
        ratings.Remove(full.PlayerPosition.Teammate());

        var context = full with { RatingsBefore = ratings };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    /// <summary>
    /// Builds a match-end context with the given pre-match ratings. The evaluated
    /// player is seated on the winning team unless <paramref name="teamWonMatch"/> is false.
    /// </summary>
    private AchievementContext BuildContext(
        int partner,
        int[] opponents,
        int self = 1500,
        bool teamWonMatch = true,
        bool matchComplete = true)
    {
        var winningSeat = _completedMatch.Winner == Team.Team1
            ? PlayerPosition.Bottom
            : PlayerPosition.Left;

        // Adjacent seats always belong to opposing teams
        var player = teamWonMatch ? winningSeat : winningSeat.Next();

        var ratings = new Dictionary<PlayerPosition, int>
        {
            [player] = self,
            [player.Teammate()] = partner,
            [player.Next()] = opponents[0],
            [player.Teammate().Next()] = opponents[1]
        };

        return new AchievementContext
        {
            Trigger = AchievementTrigger.MatchEnd,
            MatchState = matchComplete ? _completedMatch : MatchState.Create(PlayerPosition.Bottom),
            CompletedDeals = matchComplete ? _completedMatch.CompletedDeals : [],
            PlayerPosition = player,
            PlayerTeam = player.GetTeam(),
            PlayerId = Guid.NewGuid(),
            RatingsBefore = ratings,
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
