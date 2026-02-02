using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders score headers, deal results, and match status.
/// </summary>
public static class ScoreboardRenderer
{
    /// <summary>
    /// Renders the main game header with scores and current mode.
    /// </summary>
    public static void RenderHeader(MatchState matchState, GameMode? currentMode = null, MultiplierState? multiplier = null)
    {
        var team1Score = matchState.Team1MatchPoints;
        var team2Score = matchState.Team2MatchPoints;
        var target = matchState.TargetScore;

        var modeDisplay = currentMode.HasValue
            ? $" | Mode: {CardRenderer.GameModeToMarkup(currentMode.Value)}"
            : "";

        var multiplierDisplay = multiplier switch
        {
            MultiplierState.Doubled => " [yellow](Doubled)[/]",
            MultiplierState.Redoubled => " [red](Redoubled)[/]",
            _ => ""
        };

        var headerText = $"[bold]GIRETRA[/] | Score: [blue]{team1Score}[/]-[green]{team2Score}[/] | Target: {target}{modeDisplay}{multiplierDisplay}";

        AnsiConsole.Write(new Rule(headerText).RuleStyle("dim"));
    }

    /// <summary>
    /// Renders current trick points during play.
    /// </summary>
    public static void RenderTrickPoints(HandState handState)
    {
        var team1Points = handState.Team1CardPoints;
        var team2Points = handState.Team2CardPoints;
        var team1Tricks = handState.Team1TricksWon;
        var team2Tricks = handState.Team2TricksWon;

        AnsiConsole.MarkupLine($"Points: [blue]Team1: {team1Points}[/] | [green]Team2: {team2Points}[/] | Tricks: [blue]{team1Tricks}[/]-[green]{team2Tricks}[/]");
    }

    /// <summary>
    /// Renders deal result summary.
    /// </summary>
    public static void RenderDealResult(DealResult result, int dealNumber)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]DEAL #{dealNumber} RESULTS[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Team")
            .AddColumn("Card Points")
            .AddColumn("Match Points");

        var team1Style = result.AnnouncerTeam == Team.Team1 ? "blue bold" : "blue";
        var team2Style = result.AnnouncerTeam == Team.Team2 ? "green bold" : "green";

        table.AddRow(
            $"[{team1Style}]Team 1 (You + Top)[/]",
            $"[{team1Style}]{result.Team1CardPoints}[/]",
            $"[{team1Style}]+{result.Team1MatchPoints}[/]");

        table.AddRow(
            $"[{team2Style}]Team 2 (Left + Right)[/]",
            $"[{team2Style}]{result.Team2CardPoints}[/]",
            $"[{team2Style}]+{result.Team2MatchPoints}[/]");

        AnsiConsole.Write(table);

        // Additional info
        var modeText = CardRenderer.GameModeToMarkup(result.GameMode);
        var announcer = result.AnnouncerTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";

        AnsiConsole.MarkupLine($"Mode: {modeText} | Announced by: {announcer}");

        if (result.Multiplier != MultiplierState.Normal)
        {
            var multText = result.Multiplier == MultiplierState.Doubled ? "[yellow]Doubled (x2)[/]" : "[red]Redoubled (x4)[/]";
            AnsiConsole.MarkupLine($"Multiplier: {multText}");
        }

        if (result.WasSweep)
        {
            var sweeper = result.SweepingTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";
            AnsiConsole.MarkupLine($"[bold yellow]SWEEP![/] {sweeper} won all 8 tricks!");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders match final result.
    /// </summary>
    public static void RenderMatchResult(MatchState matchState)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]MATCH COMPLETE[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var winner = matchState.Winner;
        var winnerText = winner == Team.Team1
            ? "[bold blue]TEAM 1 WINS![/] (You + Top)"
            : "[bold green]TEAM 2 WINS![/] (Left + Right)";

        AnsiConsole.Write(
            new Panel(
                new Markup($"{winnerText}\n\nFinal Score: [blue]{matchState.Team1MatchPoints}[/] - [green]{matchState.Team2MatchPoints}[/]"))
            .Header("[bold]GAME OVER[/]")
            .Border(BoxBorder.Double)
            .Padding(2, 1));

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders detailed score breakdown.
    /// </summary>
    public static void RenderDetailedScore(MatchState matchState)
    {
        AnsiConsole.Write(new Rule("[bold]MATCH SCORE[/]").RuleStyle("blue"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Deal")
            .AddColumn("Mode")
            .AddColumn("Team 1")
            .AddColumn("Team 2");

        var dealNum = 1;
        foreach (var result in matchState.CompletedDeals)
        {
            table.AddRow(
                $"#{dealNum++}",
                CardRenderer.GameModeToPlainText(result.GameMode),
                $"+{result.Team1MatchPoints}",
                $"+{result.Team2MatchPoints}");
        }

        table.AddRow(
            "[bold]Total[/]",
            "",
            $"[bold blue]{matchState.Team1MatchPoints}[/]",
            $"[bold green]{matchState.Team2MatchPoints}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"Target: {matchState.TargetScore} points");
        AnsiConsole.WriteLine();
    }
}
