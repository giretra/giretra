using Giretra.Core.GameModes;
using Giretra.Manage.Validation;
using Spectre.Console;
using ValidationResult = Giretra.Manage.Validation.ValidationResult;

namespace Giretra.Manage.Output;

/// <summary>
/// Renders validation progress and results using Spectre.Console.
/// </summary>
public sealed class ValidationRenderer
{
    private readonly ValidationConfig _config;
    private readonly string _agentName;
    private readonly string _opponentName;

    public ValidationRenderer(ValidationConfig config, string agentName, string opponentName)
    {
        _config = config;
        _agentName = agentName;
        _opponentName = opponentName;
    }

    public void RenderHeader()
    {
        AnsiConsole.Write(new FigletText("Giretra").Color(Color.Blue));
        AnsiConsole.Write(new FigletText("Validate").Color(Color.Yellow));
        AnsiConsole.WriteLine();

        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        configTable.AddRow("Agent", $"[yellow]{Markup.Escape(_agentName)}[/]");
        configTable.AddRow("Opponent", $"[dim]{Markup.Escape(_opponentName)}[/]");
        configTable.AddRow("Matches", _config.MatchCount.ToString());
        configTable.AddRow("Target Score", _config.TargetScore.ToString());
        configTable.AddRow("Seed", _config.Seed?.ToString() ?? "[dim]random[/]");
        configTable.AddRow("Shuffle", _config.Shuffle ? "[yellow]enabled[/]" : "[dim]disabled[/]");
        configTable.AddRow("Timeout", _config.TimeoutMs.HasValue ? $"{_config.TimeoutMs}ms" : "[dim]none[/]");
        configTable.AddRow("Determinism Check", _config.Determinism ? "[yellow]enabled[/]" : "[dim]disabled[/]");

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();
    }

    public void RenderMatchResult(ValidationMatchProgress progress)
    {
        var statusColor = progress.Crashed ? "red" : progress.ViolationCount > 0 ? "red" : "green";
        var status = progress.Crashed ? "CRASHED" : progress.ViolationCount > 0 ? $"{progress.ViolationCount} violations" : "OK";

        AnsiConsole.MarkupLine(
            $"Match {progress.MatchNumber,3}: [{statusColor}]{status}[/] " +
            $"[dim]{progress.DealsPlayed} deals[/] " +
            $"[dim]({progress.Duration.TotalMilliseconds:F0}ms)[/]" +
            (progress.CumulativeViolations > 0 ? $" [dim](total: {progress.CumulativeViolations})[/]" : ""));
    }

    public void RenderSummary(ValidationResult result)
    {
        AnsiConsole.WriteLine();

        // Verdict
        if (result.HasViolations)
        {
            AnsiConsole.Write(new FigletText("FAIL").Color(Color.Red));
        }
        else
        {
            AnsiConsole.Write(new FigletText("PASS").Color(Color.Green));
        }

        AnsiConsole.WriteLine();

        // Overview
        RenderOverview(result);

        // Violations
        RenderViolations(result);

        // Crash details
        RenderCrashDetails(result);

        // Response time table
        RenderTimingStats(result);

        // Game mode coverage
        RenderGameModeCoverage(result);

        // Notification warnings
        RenderNotificationWarnings(result);

        // Performance trend
        RenderPerformanceTrend(result);

        // Determinism report
        RenderDeterminismReport(result);
    }

    private void RenderOverview(ValidationResult result)
    {
        var overviewPanel = new Panel(
            $"Matches Completed: [bold]{result.MatchesCompleted}[/]\n" +
            $"Matches Crashed: {(result.MatchesCrashed > 0 ? $"[red]{result.MatchesCrashed}[/]" : "[bold]0[/]")}\n" +
            $"Total Deals: [bold]{result.TotalDeals}[/]\n" +
            $"Agent Wins: [bold]{result.AgentTeamWins}[/]\n" +
            $"Opponent Wins: [bold]{result.OpponentTeamWins}[/]\n" +
            $"Total Duration: [bold]{result.TotalDuration.TotalSeconds:F2}s[/]")
            .Header("[bold]Overview[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(overviewPanel);
    }

    private void RenderViolations(ValidationResult result)
    {
        if (result.Violations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No rule violations detected.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        // Summary by type
        var violationsByType = result.Violations
            .GroupBy(v => v.Type)
            .OrderByDescending(g => g.Count());

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold red]Violations ({result.Violations.Count} total)[/]")
            .AddColumn("Type")
            .AddColumn(new TableColumn("Count").RightAligned());

        foreach (var group in violationsByType)
        {
            table.AddRow(group.Key.ToString(), $"[red]{group.Count()}[/]");
        }

        AnsiConsole.Write(table);

        // Verbose detail
        if (_config.Verbose)
        {
            AnsiConsole.WriteLine();
            var detailTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Violation Details[/]")
                .AddColumn("Match")
                .AddColumn("Deal")
                .AddColumn("Type")
                .AddColumn("Description");

            foreach (var v in result.Violations)
            {
                detailTable.AddRow(
                    v.MatchNumber.ToString(),
                    v.DealNumber.ToString(),
                    v.Type.ToString(),
                    Markup.Escape(v.Description));
            }

            AnsiConsole.Write(detailTable);
        }

        AnsiConsole.WriteLine();
    }

    private static void RenderCrashDetails(ValidationResult result)
    {
        if (result.CrashDetails.Count == 0)
            return;

        var panel = new Panel(
            string.Join("\n", result.CrashDetails.Select(d => $"[red]{Markup.Escape(d)}[/]")))
            .Header($"[bold red]Crashes ({result.MatchesCrashed})[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void RenderTimingStats(ValidationResult result)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Response Times[/]")
            .AddColumn("Decision Type")
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Min").RightAligned())
            .AddColumn(new TableColumn("Avg").RightAligned())
            .AddColumn(new TableColumn("P50").RightAligned())
            .AddColumn(new TableColumn("P95").RightAligned())
            .AddColumn(new TableColumn("P99").RightAligned())
            .AddColumn(new TableColumn("Max").RightAligned());

        foreach (var stats in result.TimingStats)
        {
            if (stats.Count == 0) continue;

            table.AddRow(
                stats.Type.ToString(),
                stats.Count.ToString(),
                FormatDuration(stats.Min),
                FormatDuration(stats.Average),
                FormatDuration(stats.P50),
                FormatDuration(stats.P95),
                FormatDuration(stats.P99),
                FormatDuration(stats.Max));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderGameModeCoverage(ValidationResult result)
    {
        var allModes = GameModeExtensions.GetAllModes().ToList();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Game Mode Coverage[/]")
            .AddColumn("Mode")
            .AddColumn("Status");

        foreach (var mode in allModes)
        {
            var covered = result.GameModesCovered.Contains(mode);
            table.AddRow(
                mode.ToString(),
                covered ? "[green]covered[/]" : "[yellow]not seen[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderNotificationWarnings(ValidationResult result)
    {
        if (result.NotificationWarnings.Count == 0)
            return;

        var byMethod = result.NotificationWarnings
            .GroupBy(w => w.MethodName)
            .OrderByDescending(g => g.Count());

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold yellow]Notification Warnings ({result.NotificationWarnings.Count})[/]")
            .AddColumn("Method")
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn("Sample Message");

        foreach (var group in byMethod)
        {
            table.AddRow(
                group.Key,
                group.Count().ToString(),
                Markup.Escape(group.First().ExceptionMessage));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderPerformanceTrend(ValidationResult result)
    {
        if (result.PerformanceTrend.Count == 0)
            return;

        var hasDegradation = result.PerformanceTrend.Any(t => t.HasDegradation);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Performance Trend (1st half vs 2nd half)[/]")
            .AddColumn("Decision Type")
            .AddColumn(new TableColumn("1st Half Median").RightAligned())
            .AddColumn(new TableColumn("2nd Half Median").RightAligned())
            .AddColumn("Status");

        foreach (var entry in result.PerformanceTrend)
        {
            var statusColor = entry.HasDegradation ? "red" : "green";
            var status = entry.HasDegradation ? "DEGRADATION" : "stable";

            table.AddRow(
                entry.Type.ToString(),
                FormatDuration(entry.FirstHalfMedian),
                FormatDuration(entry.SecondHalfMedian),
                $"[{statusColor}]{status}[/]");
        }

        AnsiConsole.Write(table);

        if (hasDegradation)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Warning: Response time degradation detected — possible memory/state leak.[/]");
        }

        AnsiConsole.WriteLine();
    }

    private void RenderDeterminismReport(ValidationResult result)
    {
        if (!_config.Determinism)
            return;

        if (result.DeterminismViolations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]Determinism check: PASS — all decisions were identical across runs.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[red]Determinism check: FAIL — {result.DeterminismViolations.Count} decision(s) differed between runs.[/]");

            if (_config.Verbose)
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[bold red]Determinism Mismatches[/]")
                    .AddColumn("Index")
                    .AddColumn("Type")
                    .AddColumn("Run 1")
                    .AddColumn("Run 2");

                foreach (var v in result.DeterminismViolations)
                {
                    table.AddRow(
                        v.DecisionIndex.ToString(),
                        v.Type.ToString(),
                        Markup.Escape(v.Run1Value),
                        Markup.Escape(v.Run2Value));
                }

                AnsiConsole.Write(table);
            }
        }

        AnsiConsole.WriteLine();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1)
            return $"{duration.TotalMicroseconds:F0}us";
        if (duration.TotalMilliseconds < 1000)
            return $"{duration.TotalMilliseconds:F1}ms";
        return $"{duration.TotalSeconds:F2}s";
    }
}
