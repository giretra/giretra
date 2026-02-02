using System.Text;
using Giretra.Core;
using Giretra.Core.Players;
using Giretra.Players;
using Giretra.UI;
using Spectre.Console;

namespace Giretra;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Enable UTF-8 output for suit symbols
        Console.OutputEncoding = Encoding.UTF8;

        // Show welcome screen
        Prompts.ShowWelcome();

        // Create players
        var humanPlayer = new HumanConsolePlayerAgent();
        var leftPlayer = new RandomPlayerAgent(PlayerPosition.Left);
        var topPlayer = new RandomPlayerAgent(PlayerPosition.Top);
        var rightPlayer = new RandomPlayerAgent(PlayerPosition.Right);

        // Choose first dealer randomly
        var positions = Enum.GetValues<PlayerPosition>();
        var firstDealer = positions[Random.Shared.Next(positions.Length)];

        AnsiConsole.MarkupLine($"[dim]First dealer: {firstDealer}[/]");
        AnsiConsole.WriteLine();

        // Create game manager
        var gameManager = new GameManager(
            humanPlayer,
            leftPlayer,
            topPlayer,
            rightPlayer,
            firstDealer);

        try
        {
            // Play the match
            var finalState = await gameManager.PlayMatchAsync();

            // The HumanConsolePlayerAgent.OnMatchEndedAsync already shows the final result
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("\n[red]An error occurred. Press Enter to exit.[/]");
            Console.ReadLine();
        }
    }
}
