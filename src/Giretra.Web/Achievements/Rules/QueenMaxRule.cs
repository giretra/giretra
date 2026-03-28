using Giretra.Core.Cards;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a deal while the player's initial 5-card hand
/// contained only Queens, Eights, and Sevens (the weakest cards).
/// </summary>
public sealed class QueenMaxRule : IAchievementRule
{
    public Guid Id => new("B7F3A1E4-6C89-4D2B-9A15-3E8F0C7D4B61");
    public string Code => "ganan_i_mama";
    public string Name => "Mpibaby varatra";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => "mama";
    public bool IsHidden => false;
    public int SortOrder => 400;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    private static readonly HashSet<CardRank> WeakRanks = [CardRank.Queen, CardRank.Eight, CardRank.Seven];

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null || context.InitialHand.Count == 0)
            return Task.FromResult(false);

        // All 5 initial cards must be Q, 8, or 7
        if (!context.InitialHand.All(c => WeakRanks.Contains(c.Rank)))
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
