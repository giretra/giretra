using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Manage.Analysis;
using Spectre.Console;

namespace Giretra.Manage.Output;

/// <summary>
/// Renders solver analysis results using Spectre.Console.
/// </summary>
public static class AnalysisRenderer
{
    public static void RenderHeader(string team1Name, string team2Name, int matchCount, int targetScore, int? seed)
    {
        AnsiConsole.Write(new FigletText("Giretra").Color(Color.Blue));
        AnsiConsole.Write(new FigletText("Analysis").Color(Color.Green));
        AnsiConsole.WriteLine();

        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        configTable.AddRow("Team 1", $"[blue]{Markup.Escape(team1Name)}[/]");
        configTable.AddRow("Team 2", $"[green]{Markup.Escape(team2Name)}[/]");
        configTable.AddRow("Matches", matchCount.ToString());
        configTable.AddRow("Target Score", targetScore.ToString());
        configTable.AddRow("Seed", seed?.ToString() ?? "[dim]random[/]");

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();
    }

    public static void RenderSummary(AnalysisResult result, int worstCount)
    {
        var team1 = result.GetQuality(Team.Team1);
        var team2 = result.GetQuality(Team.Team2);

        var summary = new Table()
            .Border(TableBorder.Double)
            .Title("[bold]Card play vs. perfect play (all hands visible)[/]")
            .AddColumn("")
            .AddColumn(new TableColumn($"[blue]{Markup.Escape(result.Team1Name)}[/]").Centered())
            .AddColumn(new TableColumn($"[green]{Markup.Escape(result.Team2Name)}[/]").Centered());

        summary.AddRow("Match wins", result.Team1Wins.ToString(), result.Team2Wins.ToString());
        summary.AddRow("Deals", team1.Deals.ToString(), team2.Deals.ToString());
        summary.AddRow("Decisions", team1.Decisions.ToString(), team2.Decisions.ToString());
        summary.AddRow("Mistakes", team1.Mistakes.ToString(), team2.Mistakes.ToString());
        summary.AddRow("Accuracy", $"{team1.Accuracy:P1}", $"{team2.Accuracy:P1}");
        summary.AddRow("[bold]Card points lost / deal[/]", $"[bold]{team1.CardPointLossPerDeal:F2}[/]", $"[bold]{team2.CardPointLossPerDeal:F2}[/]");
        summary.AddRow("[bold]Match points lost / deal[/]", $"[bold]{team1.MatchPointLossPerDeal:F2}[/]", $"[bold]{team2.MatchPointLossPerDeal:F2}[/]");

        AnsiConsole.Write(summary);
        AnsiConsole.WriteLine();

        var byMode = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Card points lost / deal, by game mode[/]")
            .AddColumn("Mode")
            .AddColumn(new TableColumn("Deals").RightAligned())
            .AddColumn(new TableColumn($"[blue]{Markup.Escape(result.Team1Name)}[/]").RightAligned())
            .AddColumn(new TableColumn($"[green]{Markup.Escape(result.Team2Name)}[/]").RightAligned());

        foreach (var mode in GameModeExtensions.GetAllModes())
        {
            var mode1 = result.GetQuality(Team.Team1, mode);
            if (mode1.Deals == 0) continue;

            var mode2 = result.GetQuality(Team.Team2, mode);
            byMode.AddRow(mode.ToString(), mode1.Deals.ToString(),
                $"{mode1.CardPointLossPerDeal:F2}", $"{mode2.CardPointLossPerDeal:F2}");
        }

        AnsiConsole.Write(byMode);
        AnsiConsole.WriteLine();

        if (worstCount > 0)
        {
            RenderWorstMistakes(result, worstCount);
        }

        var deals = Math.Max(1, result.Deals.Count);
        AnsiConsole.MarkupLine(
            $"[dim]Played in {result.PlayDuration.TotalSeconds:F1}s, solved in {result.SolverDuration.TotalSeconds:F1}s " +
            $"({result.SolverDuration.TotalMilliseconds / deals:F0}ms per deal, all cores)[/]");
    }

    private static void RenderWorstMistakes(AnalysisResult result, int count)
    {
        var worst = result.Deals
            .SelectMany(d => d.Analysis.Mistakes.Select(m => (Deal: d, Mistake: m)))
            .OrderByDescending(x => x.Mistake.MatchPointLoss ?? 0)
            .ThenByDescending(x => x.Mistake.CardPointLoss)
            .Take(count)
            .ToList();

        if (worst.Count == 0) return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Costliest mistakes[/]")
            .AddColumn("Match.Deal")
            .AddColumn("Mode")
            .AddColumn("Trick")
            .AddColumn("Player")
            .AddColumn("Played")
            .AddColumn("Best")
            .AddColumn(new TableColumn("Card pts").RightAligned())
            .AddColumn(new TableColumn("Match pts").RightAligned());

        foreach (var (deal, mistake) in worst)
        {
            var color = mistake.Player.GetTeam() == Team.Team1 ? "blue" : "green";
            table.AddRow(
                $"{deal.MatchNumber}.{deal.DealNumber}",
                deal.Result.GameMode.ToString(),
                mistake.TrickNumber.ToString(),
                $"[{color}]{mistake.Player}[/]",
                mistake.Card.ToString(),
                mistake.Evaluation.Best.Card.ToString(),
                $"-{mistake.CardPointLoss}",
                $"-{mistake.MatchPointLoss ?? 0}");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
}
