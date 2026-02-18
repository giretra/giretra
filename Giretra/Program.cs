using Giretra.Core;
using Giretra.Core.Players;
using Giretra.Players;
using Spectre.Console;

namespace Giretra;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        ShowWelcomeScreen();

        // Create players
        var human = new HumanConsolePlayerAgent(PlayerPosition.Bottom);
        var leftAgent = new CalculatingPlayerAgent(PlayerPosition.Left);
        var topAgent = new CalculatingPlayerAgent(PlayerPosition.Top);
        var rightAgent = new CalculatingPlayerAgent(PlayerPosition.Right);

        // Create game manager
        var gameManager = new GameManager(
            bottom: human,
            left: leftAgent,
            top: topAgent,
            right: rightAgent,
            firstDealer: PlayerPosition.Right);

        // Play the match
        try
        {
            var finalState = await gameManager.PlayMatchAsync();

            // Match is complete - show final screen
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Thanks for playing Giretra![/]");
            AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("[red]An error occurred. Press any key to exit.[/]");
            Console.ReadKey(true);
        }
    }

    private static void ShowWelcomeScreen()
    {
        AnsiConsole.Clear();

        AnsiConsole.Write(
            new FigletText("GIRETRA")
                .Centered()
                .Color(Color.Yellow));

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Markup("[bold]Malagasy Belote Card Game[/]")
                .Centered());
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        var panel = new Panel(
            new Markup(
                "[bold]Teams:[/]\n" +
                "  [blue]Team 1:[/] You (Bottom) + Top (Partner)\n" +
                "  [green]Team 2:[/] Left + Right\n\n" +
                "[bold]Goal:[/] First team to [yellow]150[/] match points wins!\n\n" +
                "[bold]Modes:[/] Clubs < Diamonds < Hearts < Spades < SansAs < ToutAs\n\n" +
                "[bold]Commands during play:[/]\n" +
                "  /tricks - View completed tricks\n" +
                "  /score  - View score breakdown\n" +
                "  /help   - Show all commands"))
            .Header("[bold]HOW TO PLAY[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 1);

        AnsiConsole.Write(panel);

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to start...[/]");
        Console.ReadKey(true);
    }
}
