using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.UI;
using Spectre.Console;

namespace Giretra.Input;

/// <summary>
/// Handles user prompts for card/action selection.
/// </summary>
public static class Prompts
{
    /// <summary>
    /// Prompts the user to select a card from their hand using arrow keys.
    /// Returns null if user entered a command.
    /// </summary>
    public static (Card? card, string? command) PromptCardSelection(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> validPlays,
        GameMode gameMode)
    {
        var sorted = CardSorter.SortHand(hand, gameMode);
        var validSet = validPlays.ToHashSet();
        var validCards = sorted.Where(c => validSet.Contains(c)).ToList();

        int selectedIndex = 0;

        AnsiConsole.MarkupLine("[dim]← → to select, Enter to play, /t /s /h for commands[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            // Render the horizontal card selection
            RenderCardSelection(validCards, selectedIndex, gameMode);

            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    selectedIndex = (selectedIndex - 1 + validCards.Count) % validCards.Count;
                    break;

                case ConsoleKey.RightArrow:
                    selectedIndex = (selectedIndex + 1) % validCards.Count;
                    break;

                case ConsoleKey.Enter:
                    ClearCurrentLine();
                    return (validCards[selectedIndex], null);

                case ConsoleKey.T:
                    ClearCurrentLine();
                    return (null, "/t");

                case ConsoleKey.S:
                    ClearCurrentLine();
                    return (null, "/s");

                case ConsoleKey.H:
                    ClearCurrentLine();
                    return (null, "/h");
            }
        }
    }

    private static void RenderCardSelection(IReadOnlyList<Card> cards, int selectedIndex, GameMode gameMode)
    {
        // Move cursor to beginning of line and clear
        Console.Write("\r");
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.Write("\r");

        var parts = new List<string>();
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var cardText = CardRenderer.ToMarkup(card, gameMode);

            if (i == selectedIndex)
            {
                parts.Add($"[black on yellow] {CardRenderer.ToPlainText(card)} [/]");
            }
            else
            {
                parts.Add(cardText);
            }
        }

        AnsiConsole.Markup(string.Join("  ", parts));
    }

    private static void ClearCurrentLine()
    {
        Console.Write("\r");
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.Write("\r");
    }

    /// <summary>
    /// Prompts the user to select a negotiation action.
    /// </summary>
    public static NegotiationAction PromptNegotiationAction(
        IReadOnlyList<NegotiationAction> validActions,
        PlayerPosition player)
    {
        var choices = new List<string>();
        var actionMap = new Dictionary<string, NegotiationAction>();

        foreach (var action in validActions)
        {
            var display = action switch
            {
                AnnouncementAction a => $"Announce {CardRenderer.GameModeToPlainText(a.Mode)}",
                AcceptAction => "Accept (pass)",
                DoubleAction d => $"Double {CardRenderer.GameModeToPlainText(d.TargetMode)}",
                RedoubleAction r => $"Redouble {CardRenderer.GameModeToPlainText(r.TargetMode)}",
                _ => action.ToString() ?? "Unknown"
            };

            choices.Add(display);
            actionMap[display] = action;
        }

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Choose your action:[/]")
                .PageSize(12)
                .AddChoices(choices)
                .HighlightStyle(new Style(foreground: Color.Yellow)));

        return actionMap[selection];
    }

    /// <summary>
    /// Prompts the user for the deck cut position.
    /// </summary>
    public static (int position, bool fromTop) PromptCutPosition(int deckSize)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Time to cut the deck![/]");
        AnsiConsole.MarkupLine($"[dim]The deck has {deckSize} cards. You can cut 6-26 cards from either end.[/]");
        AnsiConsole.WriteLine();

        // Choose direction first
        var direction = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Cut from which end?")
                .AddChoices("Top of deck", "Bottom of deck")
                .HighlightStyle(new Style(foreground: Color.Yellow)));

        var fromTop = direction == "Top of deck";

        // Then choose position
        var position = AnsiConsole.Prompt(
            new TextPrompt<int>($"How many cards to cut (6-26)?")
                .DefaultValue(13)
                .Validate(n =>
                {
                    if (n < 6) return ValidationResult.Error("[red]Must cut at least 6 cards[/]");
                    if (n > 26) return ValidationResult.Error("[red]Cannot cut more than 26 cards[/]");
                    return ValidationResult.Success();
                }));

        return (position, fromTop);
    }

    /// <summary>
    /// Shows a confirmation prompt with the cut details.
    /// </summary>
    public static void ShowCutResult(int position, bool fromTop)
    {
        var dirText = fromTop ? "top" : "bottom";
        AnsiConsole.MarkupLine($"[green]Cut {position} cards from the {dirText}![/]");
        AnsiConsole.WriteLine();
    }
}
