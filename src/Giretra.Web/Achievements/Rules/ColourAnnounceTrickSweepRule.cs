using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Web.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player announces a Colour mode, the negotiation ends in AllTrumps or
/// NoTrumps anyway, and the player personally wins 5+ tricks with cards of one same suit
/// different from the one they had announced.
/// </summary>
public sealed class ColourAnnounceTrickSweepRule : IAchievementRule
{
    public Guid Id => new("4C5BFC7A-6663-4100-A449-C1D173E43044");
    public string Code => "tanalahy_ny_foko";
    public string Name => "Tanalahy ny foko";
    public string Category => "style";
    public int Tier => 5;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 520;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        // The deal must have been played in AllTrumps or NoTrumps
        if (result.GameMode.GetTrumpSuit() != null)
            return Task.FromResult(false);

        // Suits the player announced as a Colour mode during negotiation
        var announcedSuits = context.NegotiationActions
            .Where(a => a.PlayerPosition == context.PlayerPosition
                && a.ActionType == RecordedActionType.Announce)
            .Select(a => a.GameMode?.GetTrumpSuit())
            .OfType<CardSuit>()
            .ToHashSet();

        if (announcedSuits.Count == 0)
            return Task.FromResult(false);

        // Player must have won 5+ tricks with cards of one same suit they did not announce
        var earned = context.Tricks
            .Where(t => t.Winner == context.PlayerPosition)
            .Select(t => t.Plays.First(p => p.Player == context.PlayerPosition).Card.Suit)
            .Where(suit => !announcedSuits.Contains(suit))
            .GroupBy(suit => suit)
            .Any(g => g.Count() >= 5);

        return Task.FromResult(earned);
    }
}
