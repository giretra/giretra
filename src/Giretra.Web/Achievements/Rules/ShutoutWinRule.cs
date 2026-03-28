using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a match while the opponent team has 0 match points.
/// </summary>
public sealed class ShutoutWinRule : IAchievementRule
{
    public Guid Id => new("B1D9E3F7-4A62-4C85-9E7B-6F2A8D5C1B34");
    public string Code => "fotsy_marik_ilay_fahadiovana";
    public string Name => "Fotsy marik'ilay fahadiovana";
    public string Category => "scoring";
    public int Tier => 2;
    public string? IconName => "trophy";
    public bool IsHidden => false;
    public int SortOrder => 130;
    public AchievementTrigger Trigger => AchievementTrigger.MatchEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var match = context.MatchState;
        if (!match.IsComplete || match.Winner == null)
            return Task.FromResult(false);

        if (match.Winner != context.PlayerTeam)
            return Task.FromResult(false);

        var opponentScore = context.PlayerTeam == Team.Team1
            ? match.Team2MatchPoints
            : match.Team1MatchPoints;

        return Task.FromResult(opponentScore == 0);
    }
}
