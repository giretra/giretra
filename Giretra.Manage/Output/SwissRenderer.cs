using Giretra.Manage.Swiss;
using Spectre.Console;

namespace Giretra.Manage.Output;

/// <summary>
/// Renders Swiss tournament progress and results using Spectre.Console.
/// </summary>
public sealed class SwissRenderer
{
    private readonly SwissConfig _config;
    private readonly IReadOnlyList<string> _participantNames;

    public SwissRenderer(SwissConfig config, IReadOnlyList<string> participantNames)
    {
        _config = config;
        _participantNames = participantNames;
    }

    public void RenderHeader()
    {
        AnsiConsole.Write(new FigletText("Giretra").Color(Color.Blue));
        AnsiConsole.Write(new FigletText("Swiss").Color(Color.Yellow));
        AnsiConsole.WriteLine();

        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        configTable.AddRow("Participants", string.Join(", ", _participantNames));
        configTable.AddRow("Rounds", _config.Rounds.ToString());
        configTable.AddRow("Target Score", _config.TargetScore.ToString());
        configTable.AddRow("Initial ELO", _config.InitialElo.ToString());
        configTable.AddRow("K-Factor", $"{_config.EloKFactorMax} → {_config.EloKFactorMin} (half-life: {_config.EloKFactorHalfLife})");
        configTable.AddRow("Margin Scoring", "[yellow]enabled[/]");
        configTable.AddRow("Seed", _config.Seed?.ToString() ?? "[dim]random[/]");
        configTable.AddRow("Shuffle", _config.Shuffle ? "[yellow]enabled[/]" : "[dim]disabled[/]");

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();
    }

    public void RenderRoundResult(SwissRoundResult round)
    {
        var parts = new List<string>();

        foreach (var match in round.Matches)
        {
            var winnerName = match.Winner.DisplayName;
            var loserName = match.Winner == match.Participant1
                ? match.Participant2.DisplayName
                : match.Participant1.DisplayName;

            parts.Add($"[bold]{winnerName}[/] > {loserName} ({match.Team1Score}-{match.Team2Score})");
        }

        var byePart = round.ByeRecipient is not null
            ? $" | Bye: [dim]{round.ByeRecipient.DisplayName}[/]"
            : "";

        AnsiConsole.MarkupLine(
            $"Round [bold]{round.RoundNumber,3}[/]: {string.Join(" | ", parts)}{byePart}");
    }

    public void RenderFinalRanking(SwissTournamentResult result)
    {
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Double)
            .Title("[bold]Swiss Tournament Results[/]")
            .AddColumn(new TableColumn("[bold]Rank[/]").Centered())
            .AddColumn(new TableColumn("[bold]Agent[/]"))
            .AddColumn(new TableColumn("[bold]Pts[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]W-L-B[/]").Centered())
            .AddColumn(new TableColumn("[bold]Win%[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]ELO[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]ELO Range[/]").Centered());

        for (int i = 0; i < result.RankedParticipants.Count; i++)
        {
            var p = result.RankedParticipants[i];
            var rank = i + 1;

            var color = rank switch
            {
                1 => "yellow",
                2 => "grey",
                3 => "orange3",
                _ => "default"
            };

            table.AddRow(
                $"[{color}]{rank}[/]",
                $"[{color}]{p.DisplayName}[/]",
                $"[bold]{p.Points}[/]",
                $"{p.Wins}-{p.Losses}-{p.Byes}",
                $"{p.WinRate:F1}%",
                $"[bold]{p.Elo:F0}[/]",
                $"{p.MinElo:F0}-{p.MaxElo:F0}");
        }

        AnsiConsole.Write(table);
    }

    public void RenderStatistics(SwissTournamentResult result)
    {
        var avgMatchTime = result.TotalMatches > 0
            ? result.TotalDuration.TotalMilliseconds / result.TotalMatches
            : 0;

        var statsPanel = new Panel(
            $"Total Rounds: [bold]{result.TotalRounds}[/]\n" +
            $"Total Matches: [bold]{result.TotalMatches}[/]\n" +
            $"Total Duration: [bold]{result.TotalDuration.TotalSeconds:F2}s[/]\n" +
            $"Avg Match Time: [bold]{avgMatchTime:F0}ms[/]")
            .Header("[bold]Statistics[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(statsPanel);
    }

    public void RenderAdjustedElo(SwissTournamentResult result)
    {
        var randomPlayer = result.RankedParticipants
            .FirstOrDefault(p => p.Name == "RandomPlayer");

        if (randomPlayer is null)
            return;

        var offset = 1000.0 - randomPlayer.Elo;

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Adjusted ELO[/] [dim](RandomPlayer = 1000)[/]")
            .AddColumn(new TableColumn("[bold]Agent[/]"))
            .AddColumn(new TableColumn("[bold]ELO[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Adjusted ELO[/]").RightAligned());

        var ranked = result.RankedParticipants
            .OrderByDescending(p => p.Elo + offset)
            .ToList();

        foreach (var p in ranked)
        {
            var adjusted = p.Elo + offset;
            table.AddRow(
                p.DisplayName,
                $"{p.Elo:F0}",
                $"[bold]{adjusted:F0}[/]");
        }

        AnsiConsole.Write(table);
    }
}
