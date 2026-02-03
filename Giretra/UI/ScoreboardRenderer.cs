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
    /// Renders deal result summary with detailed score breakdown.
    /// </summary>
    public static void RenderDealResult(DealResult result, int dealNumber)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]DEAL #{dealNumber} RESULTS[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var mode = result.GameMode;
        var category = mode.GetCategory();
        var threshold = mode.GetWinThreshold();
        var totalPoints = mode.GetTotalPoints();
        var baseMatchPoints = mode.GetBaseMatchPoints();

        var announcer = result.AnnouncerTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";
        var defender = result.AnnouncerTeam == Team.Team1 ? "[green]Team 2[/]" : "[blue]Team 1[/]";
        var announcerCardPoints = result.GetCardPoints(result.AnnouncerTeam);
        var defenderCardPoints = result.GetCardPoints(result.AnnouncerTeam == Team.Team1 ? Team.Team2 : Team.Team1);

        // Mode summary panel
        var modeInfo = new Table().Border(TableBorder.None).HideHeaders();
        modeInfo.AddColumn("");
        modeInfo.AddColumn("");
        modeInfo.AddRow("Mode:", CardRenderer.GameModeToMarkup(mode));
        modeInfo.AddRow("Total Points:", $"{totalPoints}");
        modeInfo.AddRow("Threshold:", $"{threshold}+ to win");
        modeInfo.AddRow("Announced by:", announcer);
        if (result.Multiplier != MultiplierState.Normal)
        {
            var multText = result.Multiplier == MultiplierState.Doubled ? "[yellow]Doubled (x2)[/]" : "[red]Redoubled (x4)[/]";
            modeInfo.AddRow("Multiplier:", multText);
        }

        AnsiConsole.Write(new Panel(modeInfo).Header("[dim]Game Info[/]").Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();

        // Card points breakdown
        var cardTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Team")
            .AddColumn("Role")
            .AddColumn("Card Points")
            .AddColumn("vs Threshold");

        var team1IsAnnouncer = result.AnnouncerTeam == Team.Team1;
        var team1Met = team1IsAnnouncer ? result.Team1CardPoints >= threshold : result.Team1CardPoints > (totalPoints - threshold);
        var team2Met = !team1IsAnnouncer ? result.Team2CardPoints >= threshold : result.Team2CardPoints > (totalPoints - threshold);

        cardTable.AddRow(
            "[blue]Team 1 (You + Top)[/]",
            team1IsAnnouncer ? "[yellow]Announcer[/]" : "Defender",
            $"{result.Team1CardPoints}",
            team1IsAnnouncer
                ? (result.Team1CardPoints >= threshold ? $"[green]{result.Team1CardPoints} >= {threshold}[/]" : $"[red]{result.Team1CardPoints} < {threshold}[/]")
                : $"{result.Team1CardPoints}");

        cardTable.AddRow(
            "[green]Team 2 (Left + Right)[/]",
            !team1IsAnnouncer ? "[yellow]Announcer[/]" : "Defender",
            $"{result.Team2CardPoints}",
            !team1IsAnnouncer
                ? (result.Team2CardPoints >= threshold ? $"[green]{result.Team2CardPoints} >= {threshold}[/]" : $"[red]{result.Team2CardPoints} < {threshold}[/]")
                : $"{result.Team2CardPoints}");

        AnsiConsole.Write(new Panel(cardTable).Header("[dim]Card Points[/]").Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();

        // Match points calculation
        var calcTable = new Table().Border(TableBorder.None).HideHeaders();
        calcTable.AddColumn("");
        calcTable.AddColumn("");

        if (result.WasSweep)
        {
            var sweeper = result.SweepingTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";
            calcTable.AddRow("[bold yellow]SWEEP![/]", $"{sweeper} won all 8 tricks!");

            if (result.IsInstantWin)
            {
                calcTable.AddRow("Result:", "[bold yellow]INSTANT MATCH WIN![/]");
            }
            else
            {
                var sweepBonus = mode.GetSweepBonus();
                calcTable.AddRow("Sweep Bonus:", $"{sweepBonus} match points");
                if (result.Multiplier != MultiplierState.Normal)
                {
                    var mult = result.Multiplier.GetMultiplier();
                    calcTable.AddRow("After Multiplier:", $"{sweepBonus} x {mult} = {sweepBonus * mult}");
                }
            }
        }
        else if (result.Team1CardPoints == result.Team2CardPoints)
        {
            calcTable.AddRow("Result:", "[yellow]TIE - No points awarded[/]");
        }
        else if (category == GameModeCategory.ToutAs)
        {
            // ToutAs split scoring explanation
            if (announcerCardPoints < threshold)
            {
                calcTable.AddRow("Announcer Failed:", $"{announcerCardPoints} < {threshold}");
                calcTable.AddRow("Result:", $"Defender takes all {baseMatchPoints} points");
            }
            else
            {
                var announcerRaw = (int)Math.Round(announcerCardPoints / 10.0, MidpointRounding.AwayFromZero);
                var defenderRaw = (int)Math.Round(defenderCardPoints / 10.0, MidpointRounding.AwayFromZero);

                calcTable.AddRow("Split Scoring:", $"Points / 10, rounded");
                calcTable.AddRow("Announcer:", $"{announcerCardPoints} / 10 = {announcerRaw}");
                calcTable.AddRow("Defender:", $"{defenderCardPoints} / 10 = {defenderRaw}");

                if (announcerRaw == defenderRaw)
                {
                    calcTable.AddRow("Result:", "[yellow]Rounds to tie - 0 points each[/]");
                }
            }
        }
        else
        {
            // Winner-takes-all (SansAs/Colour)
            var scoringType = category == GameModeCategory.SansAs ? "SansAs" : "Colour";
            calcTable.AddRow($"{scoringType} Scoring:", "Winner takes all");
            calcTable.AddRow("Base Points:", $"{baseMatchPoints}");

            if (announcerCardPoints >= threshold)
            {
                calcTable.AddRow("Result:", $"Announcer wins ({announcerCardPoints} >= {threshold})");
            }
            else
            {
                calcTable.AddRow("Result:", $"Announcer fails ({announcerCardPoints} < {threshold})");
                calcTable.AddRow("", $"Defender takes {baseMatchPoints} points");
            }
        }

        if (result.Multiplier != MultiplierState.Normal && !result.WasSweep && result.Team1CardPoints != result.Team2CardPoints)
        {
            var mult = result.Multiplier.GetMultiplier();
            calcTable.AddRow("Multiplier:", $"x{mult}");
        }

        AnsiConsole.Write(new Panel(calcTable).Header("[dim]Calculation[/]").Border(BoxBorder.Rounded));
        AnsiConsole.WriteLine();

        // Final match points
        var finalTable = new Table()
            .Border(TableBorder.Double)
            .AddColumn("Team")
            .AddColumn("Match Points Earned");

        var team1Style = result.Team1MatchPoints > result.Team2MatchPoints ? "blue bold" : "blue";
        var team2Style = result.Team2MatchPoints > result.Team1MatchPoints ? "green bold" : "green";

        finalTable.AddRow(
            $"[{team1Style}]Team 1 (You + Top)[/]",
            $"[{team1Style}]+{result.Team1MatchPoints}[/]");
        finalTable.AddRow(
            $"[{team2Style}]Team 2 (Left + Right)[/]",
            $"[{team2Style}]+{result.Team2MatchPoints}[/]");

        AnsiConsole.Write(new Panel(finalTable).Header("[bold]Final Result[/]").Border(BoxBorder.Rounded));
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
