using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins a NoTrumps deal with at most 3 Aces or Tens
/// among all cards won in tricks.
/// </summary>
public sealed class FaceCardWinRule : IAchievementRule
{
    public Guid Id => new("E3F7A1B5-9C24-4D68-8A3E-7B2D6F1C5E49");
    public string Code => "feno_sary_sarim_pahasambarana";
    public string Name => "Feno sary, sarim-pahasambarana";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 490;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Must be NoTrumps
        if (result.GameMode != GameMode.NoTrumps)
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Team.Team2
                : (Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Count Aces and Tens in all tricks won by the team
        var acesAndTens = context.Tricks
            .Where(t => t.Winner.GetTeam() == context.PlayerTeam)
            .SelectMany(t => t.Plays)
            .Count(p => p.Card.Rank is CardRank.Ace or CardRank.Ten);

        return Task.FromResult(acesAndTens <= 3);
    }
}
