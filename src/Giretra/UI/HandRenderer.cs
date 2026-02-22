using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders the player's hand with grouping by suit.
/// </summary>
public static class HandRenderer
{
    /// <summary>
    /// Renders a hand as a single line with cards sorted and colored.
    /// </summary>
    public static void RenderHand(
        IReadOnlyList<Card> hand,
        GameMode? gameMode,
        IReadOnlyList<Card>? validPlays = null)
    {
        var sorted = CardSorter.SortHand(hand, gameMode);
        var validSet = validPlays?.ToHashSet() ?? hand.ToHashSet();

        var cardMarkup = string.Join("  ", sorted.Select(c =>
            validSet.Contains(c)
                ? CardRenderer.ToMarkup(c, gameMode)
                : CardRenderer.ToDimmedMarkup(c)));

        AnsiConsole.Write(new Rule($"[bold]YOUR HAND[/] ({hand.Count} cards)").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"  {cardMarkup}");
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    /// <summary>
    /// Renders a compact hand display (single line).
    /// </summary>
    public static void RenderHandCompact(IReadOnlyList<Card> hand, GameMode? gameMode)
    {
        var sorted = CardSorter.SortHand(hand, gameMode);
        var markup = CardRenderer.ToMarkup(sorted, gameMode);
        AnsiConsole.MarkupLine($"Your hand: {markup}");
    }

    /// <summary>
    /// Renders opponent's hidden cards.
    /// </summary>
    public static string RenderHiddenHand(int cardCount)
    {
        return $"[dim][[{CardRenderer.RenderFaceDown(cardCount)}]][/]";
    }
}
