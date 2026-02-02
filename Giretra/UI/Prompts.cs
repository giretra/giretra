using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Encapsulates Spectre.Console prompts for user input.
/// </summary>
public static class Prompts
{
    /// <summary>
    /// Prompts the user to select a card from valid plays.
    /// </summary>
    public static Card SelectCard(IReadOnlyList<Card> validPlays, GameMode gameMode)
    {
        var choices = validPlays
            .OrderBy(c => c.Suit)
            .ThenByDescending(c => c.GetStrength(gameMode))
            .ToList();

        if (choices.Count == 1)
        {
            AnsiConsole.MarkupLine($"[dim]Only one valid play: {CardRenderer.Render(choices[0], gameMode)}[/]");
            return choices[0];
        }

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<Card>()
                .Title("Select a card to play:")
                .PageSize(10)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(choices)
                .UseConverter(c => CardRenderer.RenderPlain(c)));

        return selection;
    }

    /// <summary>
    /// Prompts the user to select a negotiation action.
    /// </summary>
    public static NegotiationAction SelectNegotiationAction(
        IReadOnlyList<NegotiationAction> validActions,
        PlayerPosition player)
    {
        if (validActions.Count == 1)
        {
            var only = validActions[0];
            AnsiConsole.MarkupLine($"[dim]Only one valid action: {FormatAction(only)}[/]");
            return only;
        }

        // Group actions by type for better presentation
        var ordered = validActions
            .OrderBy(a => a switch
            {
                AcceptAction => 0,
                AnnouncementAction => 1,
                DoubleAction => 2,
                RedoubleAction => 3,
                _ => 4
            })
            .ThenBy(a => a switch
            {
                AnnouncementAction ann => (int)ann.Mode,
                DoubleAction d => (int)d.TargetMode,
                RedoubleAction r => (int)r.TargetMode,
                _ => 0
            })
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<NegotiationAction>()
                .Title("Choose your action:")
                .PageSize(12)
                .HighlightStyle(Style.Parse("bold yellow"))
                .AddChoices(ordered)
                .UseConverter(FormatAction));

        return selection;
    }

    private static string FormatAction(NegotiationAction action) => action switch
    {
        AcceptAction => "Accept",
        AnnouncementAction a => $"Announce {CardRenderer.RenderGameModePlain(a.Mode)}",
        DoubleAction d => $"Double {CardRenderer.RenderGameModePlain(d.TargetMode)}",
        RedoubleAction r => $"Redouble {CardRenderer.RenderGameModePlain(r.TargetMode)}",
        _ => action.ToString()
    };

    /// <summary>
    /// Prompts the user to select a cut position (6-26).
    /// </summary>
    public static (int position, bool fromTop) SelectCutPosition()
    {
        AnsiConsole.MarkupLine("[bold]Time to cut the deck![/]");
        AnsiConsole.WriteLine();

        var position = AnsiConsole.Prompt(
            new TextPrompt<int>("Enter cut position (6-26):")
                .DefaultValue(16)
                .Validate(p =>
                {
                    if (p < 6 || p > 26)
                        return ValidationResult.Error("[red]Position must be between 6 and 26[/]");
                    return ValidationResult.Success();
                }));

        var fromTop = AnsiConsole.Confirm("Cut from top?", defaultValue: true);

        return (position, fromTop);
    }

    /// <summary>
    /// Waits for the user to press Enter to continue.
    /// </summary>
    public static void WaitForEnter(string message = "Press [bold]Enter[/] to continue...")
    {
        AnsiConsole.MarkupLine($"\n[dim]{message}[/]");
        Console.ReadLine();
    }

    /// <summary>
    /// Shows the welcome screen with FigletText.
    /// </summary>
    public static void ShowWelcome()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("GIRETRA")
                .Centered()
                .Color(Color.Yellow));

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Malagasy Belote[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        var infoPanel = new Panel(
            new Markup(
                "[bold]Welcome to Giretra![/]\n\n" +
                "You are playing as [blue]Bottom[/] with [blue]Top[/] as your partner.\n" +
                "Your opponents are [green]Left[/] and [green]Right[/].\n\n" +
                "[dim]First team to reach 150 points wins![/]"))
            .Border(BoxBorder.Rounded)
            .Padding(1, 1);

        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        WaitForEnter("Press [bold]Enter[/] to start the game...");
    }

    /// <summary>
    /// Shows a brief pause with a message.
    /// </summary>
    public static void ShowMessage(string message, int delayMs = 1500)
    {
        AnsiConsole.MarkupLine(message);
        Thread.Sleep(delayMs);
    }
}
