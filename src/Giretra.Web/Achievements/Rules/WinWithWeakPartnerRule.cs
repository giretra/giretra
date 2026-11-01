using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player wins a rated match against two opponents rated 1400+
/// while their partner started the match rated 1100 or below.
/// Ratings are the pre-match Elo of each seat (bots use their fixed rating).
/// </summary>
public sealed class WinWithWeakPartnerRule : IAchievementRule
{
    public Guid Id => new("A7F3C9E1-5B2D-4E86-9C14-D0B7A6F28E53");
    public string Code => "tary_somary_manky_visy_ataoko_sefo_serivisy";
    public string Name => "Tary somary manky visy ataoko sefo serivisy";
    public string Category => "milestones";
    // Production data (Nov 2026): 5 of 94 eligible humans have won an eligible match
    // with a partner rated <= 1100 (5 such wins out of 2075 eligible wins), which is
    // rarer than every existing tier-5 achievement.
    public int Tier => 5;
    public string? IconName => "trophy";
    public bool IsHidden => false;
    public int SortOrder => 230;
    public AchievementTrigger Trigger => AchievementTrigger.MatchEnd;

    public const int MaxPartnerRating = 1100;
    public const int MinOpponentRating = AchievementEvaluator.MinOpponentRating;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var match = context.MatchState;
        if (!match.IsComplete || match.Winner != context.PlayerTeam)
            return Task.FromResult(false);

        var ratings = context.RatingsBefore;
        var partner = context.PlayerPosition.Teammate();

        if (!ratings.TryGetValue(partner, out var partnerRating) || partnerRating > MaxPartnerRating)
            return Task.FromResult(false);

        foreach (var opponent in new[] { context.PlayerPosition.Next(), partner.Next() })
        {
            if (!ratings.TryGetValue(opponent, out var opponentRating) || opponentRating < MinOpponentRating)
                return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
