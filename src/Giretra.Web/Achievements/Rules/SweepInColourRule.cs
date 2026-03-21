using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team performs a sweep (all 8 tricks) in a Colour game mode.
/// </summary>
public sealed class SweepInColourRule : IAchievementRule
{
    public Guid Id => new("0E137618-5E2E-45A5-AB8B-AFBA696746BD");
    public string Code => "sweep_in_colour";
    public string Category => "scoring";
    public int Tier => 3;
    public string? IconName => "sweep";
    public bool IsHidden => false;
    public int SortOrder => 100;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        var earned = result.WasSweep
            && result.SweepingTeam == context.PlayerTeam
            && result.GameMode.GetCategory() == GameModeCategory.Colour;

        return Task.FromResult(earned);
    }
}
