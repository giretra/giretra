using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team performs a sweep in a Colour mode while doubled or redoubled.
/// </summary>
public sealed class SweepInColourDoubledRule : IAchievementRule
{
    public Guid Id => new("D5A3B9C7-2E84-4F1D-8C6A-9B4E7F2D1A83");
    public string Code => "mena_taratry_ny_afo_tsy_maty";
    public string Name => "Mena taratry ny afo tsy maty";
    public string Category => "scoring";
    public int Tier => 5;
    public string? IconName => "sweep";
    public bool IsHidden => true;
    public int SortOrder => 120;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        var earned = result.WasSweep
            && result.SweepingTeam == context.PlayerTeam
            && result.GameMode.GetCategory() == GameModeCategory.Colour
            && result.Multiplier is MultiplierState.Doubled or MultiplierState.Redoubled;

        return Task.FromResult(earned);
    }
}
