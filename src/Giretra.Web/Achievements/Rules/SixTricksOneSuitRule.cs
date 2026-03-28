using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player wins a deal and personally won 6 tricks playing cards of the same suit.
/// </summary>
public sealed class SixTricksOneSuitRule : IAchievementRule
{
    public Guid Id => new("E7B4C1A9-5D36-4F82-8A9E-1C6D3B7F2E50");
    public string Code => "mpitondra_dilizansy_eny_anosibe";
    public string Name => "Mpitondra dilizansy eny Anosibe";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 430;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Core.Players.Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Core.Players.Team.Team2
                : (Core.Players.Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Find tricks won by this player and group by the suit of the winning card
        var wonTricks = context.Tricks
            .Where(t => t.Winner == context.PlayerPosition);

        var maxSameSuit = wonTricks
            .Select(t => t.Plays.First(p => p.Player == context.PlayerPosition).Card.Suit)
            .GroupBy(suit => suit)
            .Max(g => (int?)g.Count()) ?? 0;

        return Task.FromResult(maxSameSuit >= 6);
    }
}
