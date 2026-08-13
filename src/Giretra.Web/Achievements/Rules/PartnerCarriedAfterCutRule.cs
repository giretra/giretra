using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned by the player who cut the deck when their teammate personally won 6 or more
/// tricks and their team won the deal.
/// </summary>
public sealed class PartnerCarriedAfterCutRule : IAchievementRule
{
    public Guid Id => new("8579D617-FC5D-47F1-889A-B83C70AC8D93");
    public string Code => "namoronako_ny_tsy_nananako";
    public string Name => "Namoronako ny tsy nananako";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 510;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Only the player who cut the deck can earn this
        if (context.CutterPosition != context.PlayerPosition)
            return Task.FromResult(false);

        // The player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Team.Team2
                : (Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // The teammate — not the cutter — must have won 6 or more tricks
        var teammate = context.PlayerPosition.Teammate();
        var teammateTricks = context.Tricks.Count(t => t.Winner == teammate);

        return Task.FromResult(teammateTricks >= 6);
    }
}
