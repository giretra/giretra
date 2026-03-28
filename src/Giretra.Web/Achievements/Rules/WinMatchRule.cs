namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player wins their first rated match.
/// </summary>
public sealed class WinMatchRule : IAchievementRule
{
    public Guid Id => new("78055851-CD68-4929-9EDF-C541C108B2A2");
    public string Code => "first_victory";
    public string Name => "First Victory";
    public string Category => "milestones";
    public int Tier => 1;
    public string? IconName => "trophy";
    public bool IsHidden => false;
    public int SortOrder => 10;
    public AchievementTrigger Trigger => AchievementTrigger.MatchEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var earned = context.MatchState.IsComplete
            && context.MatchState.Winner == context.PlayerTeam;

        return Task.FromResult(earned);
    }
}
