using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player personally denies the opponents' sweep by winning the last trick
/// of the deal, while their team lost all 7 previous tricks.
/// </summary>
public sealed class AvoidSweepLastTrickRule : IAchievementRule
{
    public Guid Id => new("BB2312A6-A6AE-4454-AA5A-A34317D31BE2");
    public string Code => "tsy_mba_nanisa_fa_niandry_ny_anjarany";
    public string Name => "Tsy mba nanisa fa niandry ny anjarany foana";
    public string Category => "style";
    public int Tier => 2;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 530;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        if (context.DealResult == null || context.Tricks.Count != 8)
            return Task.FromResult(false);

        var tricks = context.Tricks.OrderBy(t => t.TrickNumber).ToList();

        // The player themself must win the last trick...
        if (tricks[^1].Winner != context.PlayerPosition)
            return Task.FromResult(false);

        // ...and it must be the team's only trick (opponents took the other 7)
        var earned = tricks.Take(7).All(t => t.Winner.GetTeam() != context.PlayerTeam);

        return Task.FromResult(earned);
    }
}
