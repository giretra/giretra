using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when, in an AllTrumps or NoTrumps deal, the player's partner twice led a trick while
/// the player held a full winning suit (2+ cards, every one of them a master or a relative master)
/// that the partner could have led (they still held a card of it), led another suit instead and
/// lost the trick to the opponents.
/// </summary>
public sealed class PartnerIgnoredWinningSuitRule : IAchievementRule
{
    /// <summary>Minimum number of cards for a suit to count as a "full" winning suit.</summary>
    private const int MinSuitSize = 2;

    /// <summary>Number of lost opportunities required within one deal.</summary>
    private const int RequiredDeclines = 2;

    public Guid Id => new("6E2F0C3A-9B4D-4E71-A5C8-1D7F3B9E2A64");
    public string Code => "efa_nozakaiko_izay_rehetra_midona";
    public string Name => "Efa nozakaiko izay rehetra midona";
    public string Category => "style";
    // Production data (Nov 2026, matches since achievements went live): 12 of 94 eligible
    // humans had such a deal, about 1 completed deal in 790 per player — rarer than the
    // doubled-sweep achievements and in the middle of the tier-5 earner band (7-17).
    public int Tier => 5;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 540;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null)
            return Task.FromResult(false);

        var mode = result.GameMode;
        if (mode is not (GameMode.AllTrumps or GameMode.NoTrumps))
            return Task.FromResult(false);

        // Hands at each trick are rebuilt from the cards each player went on to play,
        // which is only exact when the whole deal is recorded.
        if (context.Tricks.Count != 8)
            return Task.FromResult(false);

        var player = context.PlayerPosition;
        var partner = player.Teammate();
        var played = new HashSet<Card>();
        var declines = 0;

        for (var i = 0; i < context.Tricks.Count; i++)
        {
            var trick = context.Tricks[i];
            var lead = trick.Plays[0];

            if (lead.Player == partner && trick.Winner.GetTeam() != context.PlayerTeam)
            {
                var remaining = context.Tricks.Skip(i).ToList();
                var hand = remaining.Select(t => t.Plays.First(p => p.Player == player).Card).ToList();
                var partnerHand = remaining.Select(t => t.Plays.First(p => p.Player == partner).Card).ToList();

                // Suits the player would run if led, and that the partner was able to lead.
                var winningSuits = hand
                    .GroupBy(c => c.Suit)
                    .Where(g => g.Count() >= MinSuitSize)
                    .Where(g => partnerHand.Any(c => c.Suit == g.Key))
                    .Where(g => g.All(c => PlayerAgentHelper.IsMasterCard(c, mode, hand, played, extended: true)))
                    .Select(g => g.Key)
                    .ToList();

                if (winningSuits.Count > 0 && !winningSuits.Contains(lead.Card.Suit))
                    declines++;
            }

            foreach (var play in trick.Plays)
                played.Add(play.Card);
        }

        return Task.FromResult(declines >= RequiredDeclines);
    }
}
