namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when a deal ends in a draw with both teams having exactly the same card points.
/// </summary>
public sealed class ExactDrawRule : IAchievementRule
{
    public Guid Id => new("D7A9C3E5-2B16-4F84-9E1D-8C4F5A3B7D62");
    public string Code => "kambana_vavy_sy_lahy";
    public string Name => "Kambana vavy sy lahy";
    public string Category => "milestones";
    public int Tier => 1;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 210;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        var earned = result.Team1CardPoints == result.Team2CardPoints
            && result.Team1MatchPoints == 0
            && result.Team2MatchPoints == 0;

        return Task.FromResult(earned);
    }
}
