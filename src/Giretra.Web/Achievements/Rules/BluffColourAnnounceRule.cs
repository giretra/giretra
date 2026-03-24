using Giretra.Core.GameModes;
using Giretra.Web.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player announces (or doubles/redoubles) a Colour mode without having any
/// trump cards in their initial 5-card hand, and their team still wins the deal.
/// </summary>
public sealed class BluffColourAnnounceRule : IAchievementRule
{
    public Guid Id => new("A9F3D7B2-6C14-4E89-8B5A-3D2E1F7C9A46");
    public string Code => "ampindramo_adalana";
    public string Name => "Ampindramo adalana";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => true;
    public int SortOrder => 440;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null || context.InitialHand.Count == 0)
            return Task.FromResult(false);

        // Must be a Colour mode
        var trumpSuit = result.GameMode.GetTrumpSuit();
        if (trumpSuit == null)
            return Task.FromResult(false);

        // Player must have announced, doubled, or redoubled (not just accepted)
        var playerActed = context.NegotiationActions.Any(a =>
            a.PlayerPosition == context.PlayerPosition
            && a.ActionType is RecordedActionType.Announce
                or RecordedActionType.Double
                or RecordedActionType.Redouble
                or RecordedActionType.ReRedouble);

        if (!playerActed)
            return Task.FromResult(false);

        // Player must not have had any trump cards in initial 5-card hand
        if (context.InitialHand.Any(c => c.Suit == trumpSuit))
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Core.Players.Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Core.Players.Team.Team2
                : (Core.Players.Team?)null;

        return Task.FromResult(winnerTeam == context.PlayerTeam);
    }
}
