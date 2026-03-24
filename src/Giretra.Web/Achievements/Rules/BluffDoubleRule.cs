using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Web.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player announces NoTrumps, an opponent escalates to AllTrumps,
/// the player's team doubles, and they win the last deal of the match.
/// </summary>
public sealed class BluffDoubleRule : IAchievementRule
{
    public Guid Id => new("B6C2D8E4-3A15-4F79-9B7C-1E5A4D9F8B32");
    public string Code => "manafosafo_sy_mividy_tena_lafo";
    public string Name => "Manafosafo sy mividy tena lafo";
    public string Category => "style";
    public int Tier => 5;
    public string? IconName => null;
    public bool IsHidden => true;
    public int SortOrder => 480;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // Must be the last deal (match is complete after this deal)
        if (!context.MatchState.IsComplete)
            return Task.FromResult(false);

        // Player's team must have won the deal
        var winnerTeam = result.Team1MatchPoints > result.Team2MatchPoints
            ? Team.Team1
            : result.Team2MatchPoints > result.Team1MatchPoints
                ? Team.Team2
                : (Team?)null;

        if (winnerTeam != context.PlayerTeam)
            return Task.FromResult(false);

        // Must be AllTrumps doubled (or redoubled)
        if (result.GameMode != GameMode.AllTrumps)
            return Task.FromResult(false);

        if (result.Multiplier is not (MultiplierState.Doubled or MultiplierState.Redoubled))
            return Task.FromResult(false);

        var actions = context.NegotiationActions;
        var opponentTeam = context.PlayerTeam == Team.Team1 ? Team.Team2 : Team.Team1;

        // Player must have announced NoTrumps
        var playerAnnouncedNoTrumps = actions.Any(a =>
            a.PlayerPosition == context.PlayerPosition
            && a.ActionType == RecordedActionType.Announce
            && a.GameMode == GameMode.NoTrumps);

        if (!playerAnnouncedNoTrumps)
            return Task.FromResult(false);

        // Opponent must have announced AllTrumps after the player's NoTrumps
        var opponentAnnouncedAllTrumps = actions.Any(a =>
            a.PlayerPosition.GetTeam() == opponentTeam
            && a.ActionType == RecordedActionType.Announce
            && a.GameMode == GameMode.AllTrumps);

        if (!opponentAnnouncedAllTrumps)
            return Task.FromResult(false);

        // Player's team must have doubled
        var teamDoubled = actions.Any(a =>
            a.PlayerPosition.GetTeam() == context.PlayerTeam
            && a.ActionType is RecordedActionType.Double or RecordedActionType.Redouble);

        return Task.FromResult(teamDoubled);
    }
}
