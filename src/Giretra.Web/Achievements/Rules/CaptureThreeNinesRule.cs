using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player captures 3 Nines from opponents in a single AllTrumps deal.
/// A Nine is "captured" when the player wins a trick containing an opponent's Nine.
/// </summary>
public sealed class CaptureThreeNinesRule : IAchievementRule
{
    public Guid Id => new("C3A7F9D1-8E52-4B64-9C1A-4D6E2F8B5A73");
    public string Code => "papango_no_namana";
    public string Name => "Papango no namana";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => true;
    public int SortOrder => 450;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        if (result.GameMode != GameMode.AllTrumps)
            return Task.FromResult(false);

        var opponentTeam = context.PlayerTeam == Team.Team1 ? Team.Team2 : Team.Team1;

        // Count opponent Nines captured in tricks won by this player
        var capturedNines = context.Tricks
            .Where(t => t.Winner == context.PlayerPosition)
            .SelectMany(t => t.Plays)
            .Count(p => p.Player.GetTeam() == opponentTeam && p.Card.Rank == CardRank.Nine);

        return Task.FromResult(capturedNines >= 3);
    }
}
