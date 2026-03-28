using Giretra.Core.Cards;
using Giretra.Core.GameModes;

namespace Giretra.Web.Achievements.Rules;

/// <summary>
/// Earned when the player saves a singleton 9 (in AllTrumps) or a singleton 10 by being void
/// in the led suit. The player or their teammate can win the trick.
/// A singleton is the only card of that suit in hand.
/// </summary>
public sealed class SaveSingletonRule : IAchievementRule
{
    public Guid Id => new("A1B5C9D3-7E42-4F86-9D2A-8C3F6E1B4D57");
    public string Code => "kamokamo_mitsoaka_raharaha";
    public string Name => "Kamokamo mitsoaka raharaha";
    public string Category => "style";
    public int Tier => 1;
    public string? IconName => null;
    public bool IsHidden => false;
    public int SortOrder => 470;
    public AchievementTrigger Trigger => AchievementTrigger.DealEnd;

    public Task<bool> IsEarnedAsync(AchievementContext context)
    {
        var result = context.DealResult;
        if (result == null || context.FullHand.Count == 0)
            return Task.FromResult(false);

        // Find singletons (only card of that suit) in the player's full hand
        var suitCounts = context.FullHand.GroupBy(c => c.Suit).ToDictionary(g => g.Key, g => g.ToList());
        var singletons = suitCounts
            .Where(kv => kv.Value.Count == 1)
            .Select(kv => kv.Value[0])
            .ToList();

        var isAllTrumps = result.GameMode == GameMode.AllTrumps;

        var teammatePosition = context.PlayerTeam == Core.Players.Team.Team1
            ? (context.PlayerPosition == Core.Players.PlayerPosition.Bottom
                ? Core.Players.PlayerPosition.Top
                : Core.Players.PlayerPosition.Bottom)
            : (context.PlayerPosition == Core.Players.PlayerPosition.Left
                ? Core.Players.PlayerPosition.Right
                : Core.Players.PlayerPosition.Left);

        foreach (var trick in context.Tricks)
        {
            // Trick must be won by the player or their teammate
            if (trick.Winner != context.PlayerPosition && trick.Winner != teammatePosition)
                continue;

            // Player must not have followed suit (void in the led suit)
            var playerPlay = trick.Plays.First(p => p.Player == context.PlayerPosition);
            var card = playerPlay.Card;
            if (card.Suit == trick.LeadSuit)
                continue;

            // Check if this card was a singleton
            if (!singletons.Any(s => s.Rank == card.Rank && s.Suit == card.Suit))
                continue;

            // Singleton 9 saved in AllTrumps, or singleton 10 saved in any mode
            if ((isAllTrumps && card.Rank == CardRank.Nine) || card.Rank == CardRank.Ten)
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
