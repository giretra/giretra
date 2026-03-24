using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a Colour mode deal while the player had no trump cards
/// in hand, and the player personally won at least one trick.
/// </summary>
public sealed class NoTrumpCardsRule : IAchievementRule
{
    public Guid Id => new("C8E1D4A6-3F97-4B5E-9C2D-7A1B8E5F3D60");
    public string Code => "tapakazo_no_mba_basy";
    public string Name => "Tapakazo no mba basy";
    public string Category => "style";
    public int Tier => 3;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 410;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null || context.FullHand.Count == 0)
            return Task.FromResult(false);

        // Must be a Colour mode
        var trumpSuit = result.GameMode.GetTrumpSuit();
        if (trumpSuit == null)
            return Task.FromResult(false);

        // Player must have had zero trump cards
        if (context.FullHand.Any(c => c.Suit == trumpSuit))
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Core.Players.Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Core.Players.Team.Team2
                : (Core.Players.Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Player must have personally won at least one trick
        if (context.Tricks.All(t => t.Winner != context.PlayerPosition))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
