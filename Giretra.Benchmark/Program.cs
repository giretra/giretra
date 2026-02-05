using Giretra.Benchmark.Benchmarking;
using Giretra.Benchmark.Factories;
using Giretra.Benchmark.Output;
using Spectre.Console;

// Parse command line arguments
var config = ParseArguments(args);

if (config is null)
{
    return 1;
}

// Create factories: DeterministicPlayer (Team1) vs CalculatingPlayer (Team2)
var team1Factory = new DeterministicPlayerAgentFactory();
var team2Factory = new CalculatingPlayerAgentFactory();

// Create runner and renderer
var runner = new BenchmarkRunner(team1Factory, team2Factory, config);
var renderer = new BenchmarkRenderer(config, team1Factory.AgentName, team2Factory.AgentName);

// Render header
renderer.RenderHeader();

// Subscribe to match completions
runner.OnMatchCompleted += renderer.RenderMatchResult;

// Run benchmark
var result = await runner.RunAsync();

// Render summary
renderer.RenderSummary(result);

return 0;

static BenchmarkConfig? ParseArguments(string[] args)
{
    var config = new BenchmarkConfig();
    var matchCount = config.MatchCount;
    var elo1 = config.Team1InitialElo;
    var elo2 = config.Team2InitialElo;
    var kFactor = config.EloKFactor;
    var targetScore = config.TargetScore;
    int? seed = config.Seed;
    var shuffle = config.Shuffle;

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-h":
            case "--help":
                PrintHelp();
                return null;

            case "-n":
            case "--matches":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var n))
                {
                    matchCount = n;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --matches requires an integer value[/]");
                    return null;
                }
                break;

            case "-t":
            case "--target":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var t))
                {
                    targetScore = t;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --target requires an integer value[/]");
                    return null;
                }
                break;

            case "--elo1":
                if (i + 1 < args.Length && double.TryParse(args[++i], out var e1))
                {
                    elo1 = e1;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --elo1 requires a numeric value[/]");
                    return null;
                }
                break;

            case "--elo2":
                if (i + 1 < args.Length && double.TryParse(args[++i], out var e2))
                {
                    elo2 = e2;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --elo2 requires a numeric value[/]");
                    return null;
                }
                break;

            case "-k":
            case "--k-factor":
                if (i + 1 < args.Length && double.TryParse(args[++i], out var k))
                {
                    kFactor = k;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --k-factor requires a numeric value[/]");
                    return null;
                }
                break;

            case "-s":
            case "--seed":
                if (i + 1 < args.Length && int.TryParse(args[++i], out var s))
                {
                    seed = s;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: --seed requires an integer value[/]");
                    return null;
                }
                break;

            case "--shuffle":
                shuffle = true;
                break;

            default:
                AnsiConsole.MarkupLine($"[yellow]Warning: Unknown argument '{args[i]}'[/]");
                break;
        }
    }

    return new BenchmarkConfig
    {
        MatchCount = matchCount,
        Team1InitialElo = elo1,
        Team2InitialElo = elo2,
        EloKFactor = kFactor,
        TargetScore = targetScore,
        Seed = seed,
        Shuffle = shuffle
    };
}

static void PrintHelp()
{
    AnsiConsole.Write(new FigletText("Giretra").Color(Color.Blue));
    AnsiConsole.Write(new FigletText("Benchmark").Color(Color.Green));
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[bold]Usage:[/] dotnet run --project Giretra.Benchmark [[OPTIONS]]");
    AnsiConsole.WriteLine();

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Option")
        .AddColumn("Description")
        .AddColumn("Default");

    table.AddRow("-n, --matches", "Number of matches to play", "1000");
    table.AddRow("-t, --target", "Target score to win a match", "500");
    table.AddRow("-s, --seed", "Random seed for reproducibility", "random");
    table.AddRow("--shuffle", "Shuffle deck each match (for deterministic agents)", "off");
    table.AddRow("--elo1", "Initial ELO for Team 1", "1200");
    table.AddRow("--elo2", "Initial ELO for Team 2", "1200");
    table.AddRow("-k, --k-factor", "ELO K-factor", "24");
    table.AddRow("-h, --help", "Show this help message", "");

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[bold]Examples:[/]");
    AnsiConsole.MarkupLine("  dotnet run --project Giretra.Benchmark");
    AnsiConsole.MarkupLine("  dotnet run --project Giretra.Benchmark -- -n 50");
    AnsiConsole.MarkupLine("  dotnet run --project Giretra.Benchmark -- --elo1 1300 --elo2 1100");
}
