using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team sweeps all 8 tricks and the tricks are shared
/// evenly: the player wins exactly 4 and their partner wins the other 4.
/// </summary>
public sealed class BalancedSweepRule : IAchievementRule
{
    public Guid Id => new("533C4436-B8AC-4297-849B-DFC520B3CDFC");
    public string Code => "rano_sy_vary";
    public string Name => "Rano sy vary";
    public string Category => "scoring";
    // Production data (Nov 2026): a 4/4 sweep happens in ~1 of 65 completed deals,
    // slightly more often than sweep_in_colour (tier 3) and far more often than the
    // doubled-sweep achievements (tier 5).
    public int Tier => 3;
    public string? IconName => "sweep";
    public bool IsHidden => false;
    public int SortOrder => 140;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        if (!result.WasSweep || result.SweepingTeam != context.PlayerTeam)
            return Task.FromResult(false);

        if (context.Tricks.Count != 8)
            return Task.FromResult(false);

        var ownTricks = context.Tricks.Count(t => t.Winner == context.PlayerPosition);
        var partnerTricks = context.Tricks.Count(t => t.Winner == context.PlayerPosition.Teammate());

        return Task.FromResult(ownTricks == 4 && partnerTricks == 4);
    }
}
