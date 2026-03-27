using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a sweep in NoTrumps while doubled or redoubled.
/// </summary>
public sealed class SweepNoTrumpsDoubledRule : IAchievementRule
{
    public Guid Id => new("A4D2F8B1-7E53-4C96-8B1A-2F9E6D3C5A47");
    public string Code => "azafady_kely_tompoko";
    public string Name => "Azafady kely tompoko";
    public string Category => "scoring";
    public int Tier => 5;
    public string? IconName => "sweep";
    public bool IsHidden => false;
    public int SortOrder => 110;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        var earned = result.WasSweep
            && result.SweepingTeam == context.PlayerTeam
            && result.GameMode == GameMode.NoTrumps
            && result.Multiplier is MultiplierState.Doubled or MultiplierState.Redoubled;

        return Task.FromResult(earned);
    }
}
