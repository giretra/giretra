using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player's team wins an AllTrumps deal without either teammate holding any Jack.
/// </summary>
public sealed class NoJackAllTrumpsRule : IAchievementRule
{
    public Guid Id => new("F2C8A6D4-1B73-4E95-9D3F-5A8C2E7B4F19");
    public string Code => "mbola_tsy_bedy";
    public string Name => "Mbola tsy bedy";
    public string Category => "style";
    public int Tier => 5;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 420;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null || context.FullHand.Count == 0)
            return Task.FromResult(false);

        // Must be AllTrumps
        if (result.GameMode != GameMode.AllTrumps)
            return Task.FromResult(false);

        // Player's team must have won the deal (more match points than opponent)
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Core.Players.Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Core.Players.Team.Team2
                : (Core.Players.Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Neither teammate must have had a Jack — check via tricks played by the team
        var teamPlays = context.Tricks
            .SelectMany(t => t.Plays)
            .Where(p => p.Player.GetTeam() == context.PlayerTeam);

        if (teamPlays.Any(p => p.Card.Rank == CardRank.Jack))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
