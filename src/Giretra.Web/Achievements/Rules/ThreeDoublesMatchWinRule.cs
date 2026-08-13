using Giretra.Web.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins the match and the player personally Doubled,
/// Redoubled or ReRedoubled in at least three deals of that match.
/// </summary>
public sealed class ThreeDoublesMatchWinRule : IAchievementRule
{
    public Guid Id => new("84FB48C5-4722-4A66-85F7-6D1CEAF4B0B6");
    public string Code => "asio_siramamy";
    public string Name => "Asio siramamy";
    public string Category => "milestones";
    public int Tier => 4;
    public string? IconName => "trophy";
    public bool IsHidden => false;
    public int SortOrder => 220;
    public AchievementTrigger Trigger => AchievementTrigger.MatchEnd;

    /// <summary>Deals in which the player must have challenged.</summary>
    private const int RequiredDeals = 3;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var match = context.MatchState;
        if (!match.IsComplete || match.Winner != context.PlayerTeam)
            return Task.FromResult(false);

        // Count deals where this player challenged, not the number of challenges:
        // several challenges in one deal still count once.
        var challengedDeals = context.MatchNegotiations
            .Count(deal => deal.Actions.Any(a =>
                a.PlayerPosition == context.PlayerPosition
                && a.ActionType is RecordedActionType.Double
                    or RecordedActionType.Redouble
                    or RecordedActionType.ReRedouble));

        return Task.FromResult(challengedDeals >= RequiredDeals);
    }
}
