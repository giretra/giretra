using Giretra.Core.State;
using Giretra.UI;
using Spectre.Console;

namespace Giretra.Input;

/// <summary>
/// Handles slash commands during gameplay.
/// </summary>
public static class CommandHandler
{
    /// <summary>
    /// Processes a command and returns true if the game should redraw.
    /// </summary>
    public static bool HandleCommand(string command, HandState? handState, MatchState matchState)
    {
        var cmd = command.ToLowerInvariant().TrimStart('/');

        return cmd switch
        {
            "tricks" or "t" => HandleTricksCommand(handState),
            "score" or "s" => HandleScoreCommand(matchState),
            "help" or "h" or "?" => HandleHelpCommand(),
            _ => HandleUnknownCommand(command)
        };
    }

    private static bool HandleTricksCommand(HandState? handState)
    {
        if (handState == null)
        {
            AnsiConsole.MarkupLine("[red]No hand in progress.[/]");
            Console.ReadKey(true);
            return true;
        }

        TrickHistoryViewer.RenderTrickHistory(handState);
        return true;
    }

    private static bool HandleScoreCommand(MatchState matchState)
    {
        AnsiConsole.Clear();
        ScoreboardRenderer.RenderDetailedScore(matchState);
        AnsiConsole.MarkupLine("[dim]Press any key to return...[/]");
        Console.ReadKey(true);
        return true;
    }

    private static bool HandleHelpCommand()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold]COMMANDS[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Command")
            .AddColumn("Alias")
            .AddColumn("Description");

        table.AddRow("/tricks", "/t", "View completed tricks history");
        table.AddRow("/score", "/s", "View detailed score breakdown");
        table.AddRow("/help", "/h, /?", "Show this help");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]GAME RULES[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Teams:[/]");
        AnsiConsole.MarkupLine("  [blue]Team 1:[/] You (Bottom) + Top (Partner)");
        AnsiConsole.MarkupLine("  [green]Team 2:[/] Left + Right");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Card Colors:[/]");
        AnsiConsole.MarkupLine("  [red]Red[/]: Hearts and Diamonds");
        AnsiConsole.MarkupLine("  White: Spades and Clubs");
        AnsiConsole.MarkupLine("  [bold yellow]Yellow[/]: Trump cards");
        AnsiConsole.MarkupLine("  [dim]Dim[/]: Cards you cannot play");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Modes (lowest to highest):[/]");
        AnsiConsole.MarkupLine("  1. Colour (Clubs/Diamonds/Hearts/Spades) - One suit is trump");
        AnsiConsole.MarkupLine("  2. SansAs - No trump, standard ranking");
        AnsiConsole.MarkupLine("  3. ToutAs - No trump, trump ranking for all");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]Win Condition:[/]");
        AnsiConsole.MarkupLine("  First team to 150 match points wins!");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]Press any key to return...[/]");
        Console.ReadKey(true);
        return true;
    }

    private static bool HandleUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
        AnsiConsole.MarkupLine("[dim]Type /help for available commands.[/]");
        Console.ReadKey(true);
        return true;
    }
}
