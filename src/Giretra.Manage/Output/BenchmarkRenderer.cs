using Giretra.Manage.Benchmarking;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Spectre.Console;

namespace Giretra.Manage.Output;

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
        configTable.AddRow("Shuffle", _config.Shuffle ? "[yellow]enabled[/]" : "[dim]disabled[/]");

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
    public void RenderSummary(BenchmarkResult result, string? team1AgentName = null, string? team2AgentName = null)
    {
        AnsiConsole.WriteLine();

        // Main results table
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

        // Confidence intervals
        var ci1 = result.Team1WinRateConfidenceInterval;
        var ci2 = result.Team2WinRateConfidenceInterval;

        summaryTable.AddRow(
            "95% CI",
            $"[blue]{ci1.Lower:P1} - {ci1.Upper:P1}[/]",
            $"[green]{ci2.Lower:P1} - {ci2.Upper:P1}[/]");

        summaryTable.AddRow(
            "Initial ELO",
            $"{result.Team1InitialElo:F0}",
            $"{result.Team2InitialElo:F0}");

        summaryTable.AddRow(
            "Final ELO",
            FormatEloChange(result.Team1FinalElo, result.Team1InitialElo, "blue"),
            FormatEloChange(result.Team2FinalElo, result.Team2InitialElo, "green"));

        summaryTable.AddRow(
            "ELO Range",
            $"[blue]{result.Team1MinElo:F0} - {result.Team1MaxElo:F0}[/]",
            $"[green]{result.Team2MinElo:F0} - {result.Team2MaxElo:F0}[/]");

        summaryTable.AddRow(
            "ELO Change",
            FormatEloChangeDelta(result.Team1FinalElo - result.Team1InitialElo),
            FormatEloChangeDelta(result.Team2FinalElo - result.Team2InitialElo));

        AnsiConsole.Write(summaryTable);

        // Statistical significance panel
        var significanceColor = result.IsSignificant ? "yellow" : "dim";
        var significancePanel = new Panel(
            $"P-value: [bold]{result.PValue:F4}[/]\n" +
            $"Result: [{significanceColor}]{result.SignificanceInterpretation}[/]\n" +
            $"Conclusion: {(result.IsSignificant ? "[yellow]Win rates differ significantly from 50%[/]" : "[dim]No significant difference from 50% (agents are equivalent)[/]")}")
            .Header("[bold]Statistical Significance[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(significancePanel);

        // General statistics panel
        var statsPanel = new Panel(
            $"Total Matches: [bold]{result.Matches.Count}[/]\n" +
            $"Total Deals: [bold]{result.TotalDeals}[/]\n" +
            $"Avg Deals/Match: [bold]{result.AverageDealsPerMatch:F1}[/]\n" +
            $"Total Duration: [bold]{result.TotalDuration.TotalSeconds:F2}s[/]\n" +
            $"Avg Match Time: [bold]{result.TotalDuration.TotalMilliseconds / result.Matches.Count:F0}ms[/]")
            .Header("[bold]Statistics[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(statsPanel);

        // Game mode win rate breakdown
        RenderGameModeStats(result);

        // Adjusted ELO (normalized so RandomPlayer = 1000)
        RenderAdjustedElo(result, team1AgentName, team2AgentName);
    }

    private void RenderGameModeStats(BenchmarkResult result)
    {
        var stats = result.GetGameModeStats();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Win Rate by Game Mode[/]")
            .AddColumn(new TableColumn("[bold]Game Mode[/]"))
            .AddColumn(new TableColumn("[bold]Announcer[/]"))
            .AddColumn(new TableColumn("[bold]Deals[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Ann. Win Rate[/]").RightAligned());

        // Aggregate all colour modes into a single row
        var colourStats = stats.Where(s => s.GameMode is GameMode.ColourClubs or GameMode.ColourDiamonds
            or GameMode.ColourHearts or GameMode.ColourSpades).ToList();
        var colourAggregated = new GameModeStats(
            GameMode.ColourClubs,
            colourStats.Sum(s => s.TotalDeals),
            new AnnouncerStats(
                colourStats.Sum(s => s.Team1Announced.Announced),
                colourStats.Sum(s => s.Team1Announced.AnnouncerWins)),
            new AnnouncerStats(
                colourStats.Sum(s => s.Team2Announced.Announced),
                colourStats.Sum(s => s.Team2Announced.AnnouncerWins)));

        var displayRows = new (string Name, GameModeStats Stats)[]
        {
            ("Colour", colourAggregated),
            ("No Trumps", stats.First(s => s.GameMode == GameMode.NoTrumps)),
            ("All Trumps", stats.First(s => s.GameMode == GameMode.AllTrumps))
        };

        foreach (var (name, s) in displayRows)
        {
            if (s.TotalDeals == 0)
            {
                table.AddRow(name, "", "[dim]0[/]", "[dim]-[/]");
                continue;
            }

            table.AddRow(
                name,
                $"[blue]{_team1Name}[/]",
                FormatAnnouncerDeals(s.Team1Announced),
                FormatAnnouncerWinRate(s.Team1Announced, "blue"));

            table.AddRow(
                "",
                $"[green]{_team2Name}[/]",
                FormatAnnouncerDeals(s.Team2Announced),
                FormatAnnouncerWinRate(s.Team2Announced, "green"));
        }

        // Total row
        var totalTeam1 = stats.Aggregate(
            new AnnouncerStats(0, 0),
            (acc, s) => new AnnouncerStats(
                acc.Announced + s.Team1Announced.Announced,
                acc.AnnouncerWins + s.Team1Announced.AnnouncerWins));
        var totalTeam2 = stats.Aggregate(
            new AnnouncerStats(0, 0),
            (acc, s) => new AnnouncerStats(
                acc.Announced + s.Team2Announced.Announced,
                acc.AnnouncerWins + s.Team2Announced.AnnouncerWins));

        table.AddEmptyRow();
        table.AddRow(
            "[bold]Total[/]",
            $"[bold blue]{_team1Name}[/]",
            $"[bold]{totalTeam1.Announced}[/]",
            $"[bold blue]{totalTeam1.AnnouncerWinRate:P1}[/]");
        table.AddRow(
            "",
            $"[bold green]{_team2Name}[/]",
            $"[bold]{totalTeam2.Announced}[/]",
            $"[bold green]{totalTeam2.AnnouncerWinRate:P1}[/]");

        AnsiConsole.Write(table);
    }

    private static string FormatAnnouncerDeals(AnnouncerStats stats)
        => stats.Announced == 0 ? "[dim]0[/]" : stats.Announced.ToString();

    private static string FormatAnnouncerWinRate(AnnouncerStats stats, string color)
        => stats.Announced == 0 ? "[dim]-[/]" : $"[{color}]{stats.AnnouncerWinRate:P1}[/]";

    private static string FormatGameMode(GameMode mode)
        => mode switch
        {
            GameMode.ColourClubs => "Clubs",
            GameMode.ColourDiamonds => "Diamonds",
            GameMode.ColourHearts => "Hearts",
            GameMode.ColourSpades => "Spades",
            GameMode.NoTrumps => "No Trumps",
            GameMode.AllTrumps => "All Trumps",
            _ => mode.ToString()
        };

    private static void RenderAdjustedElo(BenchmarkResult result, string? team1AgentName, string? team2AgentName)
    {
        const string randomPlayer = "RandomPlayer";

        double? randomElo = null;
        if (team1AgentName == randomPlayer)
            randomElo = result.Team1FinalElo;
        else if (team2AgentName == randomPlayer)
            randomElo = result.Team2FinalElo;

        if (randomElo is null)
            return;

        var offset = 1000.0 - randomElo.Value;

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Adjusted ELO[/] [dim](RandomPlayer = 1000)[/]")
            .AddColumn(new TableColumn("[bold]Team[/]"))
            .AddColumn(new TableColumn("[bold]ELO[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Adjusted ELO[/]").RightAligned());

        var teams = new[]
        {
            (Name: result.Team1Name, Elo: result.Team1FinalElo),
            (Name: result.Team2Name, Elo: result.Team2FinalElo)
        };

        foreach (var (name, elo) in teams.OrderByDescending(t => t.Elo + offset))
        {
            var adjusted = elo + offset;
            table.AddRow(name, $"{elo:F0}", $"[bold]{adjusted:F0}[/]");
        }

        AnsiConsole.Write(table);
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
