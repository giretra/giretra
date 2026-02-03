using Giretra.Benchmark.Benchmarking;
using Giretra.Core.Players;
using Spectre.Console;

namespace Giretra.Benchmark.Output;

/// <summary>
/// Renders benchmark progress and results using Spectre.Console.
/// </summary>
public sealed class BenchmarkRenderer
{
    private readonly BenchmarkConfig _config;
    private readonly string _team1Name;
    private readonly string _team2Name;

    public BenchmarkRenderer(BenchmarkConfig config, string team1Name, string team2Name)
    {
        _config = config;
        _team1Name = team1Name;
        _team2Name = team2Name;
    }

    /// <summary>
    /// Renders the header at benchmark start.
    /// </summary>
    public void RenderHeader()
    {
        AnsiConsole.Write(new FigletText("Giretra").Color(Color.Blue));
        AnsiConsole.Write(new FigletText("Benchmark").Color(Color.Green));
        AnsiConsole.WriteLine();

        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        configTable.AddRow("Team 1", $"[blue]{_team1Name}[/] (ELO: {_config.Team1InitialElo})");
        configTable.AddRow("Team 2", $"[green]{_team2Name}[/] (ELO: {_config.Team2InitialElo})");
        configTable.AddRow("Matches", _config.MatchCount.ToString());
        configTable.AddRow("Target Score", _config.TargetScore.ToString());
        configTable.AddRow("K-Factor", _config.EloKFactor.ToString());
        configTable.AddRow("Seed", _config.Seed?.ToString() ?? "[dim]random[/]");

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders a single match result line.
    /// </summary>
    public void RenderMatchResult(MatchResult result)
    {
        var winnerName = result.Winner == Team.Team1 ? _team1Name : _team2Name;
        var winnerColor = result.Winner == Team.Team1 ? "blue" : "green";
        var eloChangeSign = result.Team1EloChange >= 0 ? "+" : "";
        var eloChangeColor = result.Team1EloChange >= 0 ? "blue" : "green";

        AnsiConsole.MarkupLine(
            $"Match {result.MatchNumber,3}: [{winnerColor}]{winnerName}[/] wins " +
            $"({result.Team1FinalScore}-{result.Team2FinalScore}) " +
            $"[dim]{result.DealsPlayed} deals[/] " +
            $"[{eloChangeColor}]ELO {eloChangeSign}{result.Team1EloChange:F1}[/] " +
            $"[dim]({result.Duration.TotalMilliseconds:F0}ms)[/]");
    }

    /// <summary>
    /// Renders the final summary.
    /// </summary>
    public void RenderSummary(BenchmarkResult result)
    {
        AnsiConsole.WriteLine();

        var summaryTable = new Table()
            .Border(TableBorder.Double)
            .Title("[bold]Benchmark Results[/]")
            .AddColumn("")
            .AddColumn(new TableColumn($"[blue]{result.Team1Name}[/]").Centered())
            .AddColumn(new TableColumn($"[green]{result.Team2Name}[/]").Centered());

        summaryTable.AddRow(
            "Wins",
            $"[blue]{result.Team1Wins}[/]",
            $"[green]{result.Team2Wins}[/]");

        summaryTable.AddRow(
            "Win Rate",
            $"[blue]{result.Team1WinRate:P1}[/]",
            $"[green]{result.Team2WinRate:P1}[/]");

        summaryTable.AddRow(
            "Initial ELO",
            $"{result.Team1InitialElo:F0}",
            $"{result.Team2InitialElo:F0}");

        summaryTable.AddRow(
            "Final ELO",
            FormatEloChange(result.Team1FinalElo, result.Team1InitialElo, "blue"),
            FormatEloChange(result.Team2FinalElo, result.Team2InitialElo, "green"));

        summaryTable.AddRow(
            "ELO Change",
            FormatEloChangeDelta(result.Team1FinalElo - result.Team1InitialElo),
            FormatEloChangeDelta(result.Team2FinalElo - result.Team2InitialElo));

        AnsiConsole.Write(summaryTable);

        var statsPanel = new Panel(
            $"Total Matches: [bold]{result.Matches.Count}[/]\n" +
            $"Total Deals: [bold]{result.TotalDeals}[/]\n" +
            $"Avg Deals/Match: [bold]{result.AverageDealsPerMatch:F1}[/]\n" +
            $"Total Duration: [bold]{result.TotalDuration.TotalSeconds:F2}s[/]\n" +
            $"Avg Match Time: [bold]{result.TotalDuration.TotalMilliseconds / result.Matches.Count:F0}ms[/]")
            .Header("[bold]Statistics[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(statsPanel);
    }

    private static string FormatEloChange(double finalElo, double initialElo, string color)
    {
        var change = finalElo - initialElo;
        var arrow = change >= 0 ? "^" : "v";
        return $"[{color}]{finalElo:F0}[/] ({arrow}{Math.Abs(change):F0})";
    }

    private static string FormatEloChangeDelta(double change)
    {
        var sign = change >= 0 ? "+" : "";
        var color = change >= 0 ? "blue" : "green";
        return $"[{color}]{sign}{change:F0}[/]";
    }
}
