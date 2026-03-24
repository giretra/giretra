using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player captures 3 Tens from opponents in a single NoTrumps deal.
/// </summary>
public sealed class CaptureThreeTensRule : IAchievementRule
{
    public Guid Id => new("F8D2B5A3-9C17-4E63-8A4F-6B1E3D7C2F98");
    public string Code => "mpitsapa_lanitra_aminy_tanana";
    public string Name => "Mpitsapa lanitra aminy tanana";
    public string Category => "style";
    public int Tier => 4;
    public string? IconName => null;
    public bool IsHidden => true;
    public int SortOrder => 460;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        if (result.GameMode != GameMode.NoTrumps)
            return Task.FromResult(false);

        var opponentTeam = context.PlayerTeam == Team.Team1 ? Team.Team2 : Team.Team1;

        var capturedTens = context.Tricks
            .Where(t => t.Winner == context.PlayerPosition)
            .SelectMany(t => t.Plays)
            .Count(p => p.Player.GetTeam() == opponentTeam && p.Card.Rank == CardRank.Ten);

        return Task.FromResult(capturedTens >= 3);
    }
}
