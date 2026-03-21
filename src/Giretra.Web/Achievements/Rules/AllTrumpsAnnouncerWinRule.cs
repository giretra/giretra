using Giretra.Core.GameModes;
using Giretra.Web.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player announces AllTrumps and their team wins the deal.
/// Uses negotiation history to verify the player was the one who announced.
/// </summary>
public sealed class AllTrumpsAnnouncerWinRule : IAchievementRule
{
    public Guid Id => new("38C3CF51-DF52-49D2-BE92-747B7888A3DA");
    public string Code => "alltrumps_announcer_win";
    public string Category => "modes";
    public int Tier => 2;
    public string? IconName => "alltrumps";
    public bool IsHidden => false;
    public int SortOrder => 200;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Must be AllTrumps and announcer's team won
        if (result.GameMode != GameMode.AllTrumps || !result.AnnouncerWon)
            return Task.FromResult(false);

        // The player must have been the one who announced AllTrumps
        var announcedAllTrumps = context.NegotiationActions.Any(a =>
            a.ActionType == RecordedActionType.Announce
            && a.PlayerPosition == context.PlayerPosition
            && a.GameMode == GameMode.AllTrumps);

        return Task.FromResult(announcedAllTrumps);
    }
}
