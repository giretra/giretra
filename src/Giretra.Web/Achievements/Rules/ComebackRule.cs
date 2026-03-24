using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a match after being down 0-124 or worse in match points.
/// </summary>
public sealed class ComebackRule : IAchievementRule
{
    public Guid Id => new("D4E8A2C6-7F31-4B95-9A6D-8C5B3E1F7D29");
    public string Code => "mpamadika_basy_marovava";
    public string Name => "Mpamadika basy marovava";
    public string Category => "milestones";
    public int Tier => 3;
    public string? IconName => "trophy";
    public bool IsHidden => true;
    public int SortOrder => 200;
    public AchievementTrigger Trigger => AchievementTrigger.MatchEnd;

    private const int DeficitThreshold = 124;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var match = context.MatchState;
        if (!match.IsComplete || match.Winner != context.PlayerTeam)
            return Task.FromResult(false);

        // Reconstruct running scores to check if team was ever down 0-124+
        var teamScore = 0;
        var opponentScore = 0;

        foreach (var deal in context.CompletedDeals)
        {
            var teamPoints = context.PlayerTeam == Team.Team1
                ? deal.Team1MatchPoints
                : deal.Team2MatchPoints;
            var opponentPoints = context.PlayerTeam == Team.Team1
                ? deal.Team2MatchPoints
                : deal.Team1MatchPoints;

            teamScore += teamPoints;
            opponentScore += opponentPoints;

            if (teamScore == 0 && opponentScore >= DeficitThreshold)
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
