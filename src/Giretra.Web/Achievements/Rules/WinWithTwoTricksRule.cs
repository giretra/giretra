using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a deal while only winning 2 tricks.
/// </summary>
public sealed class WinWithTwoTricksRule : IAchievementRule
{
    public Guid Id => new("A5C8E2F6-1D47-4B93-8F3A-9E7B6C4D2A18");
    public string Code => "laoka_na_vary_tena_tsara_sy_lafo";
    public string Name => "Laoka sy vary, tena tsara sy lafo";
    public string Category => "style";
    public int Tier => 3;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 500;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Team.Team2
                : (Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Team must have won exactly 2 tricks
        var teamTricks = context.Tricks.Count(t => t.Winner.GetTeam() == context.PlayerTeam);

        return Task.FromResult(teamTricks == 2);
    }
}
