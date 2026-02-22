using System.ComponentModel;
using Giretra.Manage.Benchmarking;
using Giretra.Manage.Data;
using Giretra.Manage.Discovery;
using Giretra.Manage.Output;
using Giretra.Core.Players;
using Giretra.Core.Players.Factories;
using Giretra.Model;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Giretra.Manage.Commands;

public sealed class BenchmarkSettings : CommandSettings
{
    [CommandArgument(0, "[team1]")]
    [Description("Team 1 agent by AgentName or DisplayName (default: DeterministicPlayer)")]
    public string? Team1 { get; init; }

    [CommandArgument(1, "[team2]")]
    [Description("Team 2 agent by AgentName or DisplayName (default: CalculatingPlayer)")]
    public string? Team2 { get; init; }

    [CommandOption("-n|--matches")]
    [Description("Number of matches to play")]
    [DefaultValue(1000)]
    public int MatchCount { get; init; } = 1000;

    [CommandOption("-t|--target")]
    [Description("Target score to win a match")]
    [DefaultValue(500)]
    public int TargetScore { get; init; } = 500;

    [CommandOption("-s|--seed")]
    [Description("Random seed for reproducibility")]
    public int? Seed { get; init; }

    [CommandOption("--shuffle")]
    [Description("Shuffle deck each match (for deterministic agents)")]
    [DefaultValue(true)]
    public bool Shuffle { get; init; } = true;

    [CommandOption("--elo1")]
    [Description("Initial ELO for Team 1")]
    [DefaultValue(1200.0)]
    public double Elo1 { get; init; } = 1200;

    [CommandOption("--elo2")]
    [Description("Initial ELO for Team 2")]
    [DefaultValue(1200.0)]
    public double Elo2 { get; init; } = 1200;

    [CommandOption("-k|--k-factor")]
    [Description("ELO K-factor")]
    [DefaultValue(24.0)]
    public double KFactor { get; init; } = 24;

    [CommandOption("--connection-string")]
    [Description("PostgreSQL connection string (default: from environment)")]
    public string? ConnectionString { get; init; }
}

public sealed class BenchmarkCommand : AsyncCommand<BenchmarkSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BenchmarkSettings settings, CancellationToken cancellation)
    {
        var config = new BenchmarkConfig
        {
            MatchCount = settings.MatchCount,
            TargetScore = settings.TargetScore,
            Seed = settings.Seed,
            Shuffle = settings.Shuffle,
            Team1InitialElo = settings.Elo1,
            Team2InitialElo = settings.Elo2,
            EloKFactor = settings.KFactor
        };

        IPlayerAgentFactory team1Factory;
        IPlayerAgentFactory team2Factory;

        if (settings.Team1 is not null || settings.Team2 is not null)
        {
            var available = FactoryDiscovery.DiscoverAll();
            var names = new[] { settings.Team1 ?? "DeterministicPlayer", settings.Team2 ?? "CalculatingPlayer" };
            var resolved = FactoryDiscovery.Resolve(names, available);
            team1Factory = resolved[0];
            team2Factory = resolved[1];
        }
        else
        {
            team1Factory = new DeterministicPlayerAgentFactory();
            team2Factory = new CalculatingPlayerAgentFactory();
        }

        var runner = new BenchmarkRunner(team1Factory, team2Factory, config);
        var renderer = new BenchmarkRenderer(config, team1Factory.DisplayName, team2Factory.DisplayName);

        try
        {
            await runner.InitializeAsync(cancellation);

            renderer.RenderHeader();
            runner.OnMatchCompleted += renderer.RenderMatchResult;

            var result = await runner.RunAsync();
            renderer.RenderSummary(result, team1Factory.AgentName, team2Factory.AgentName);

            var adjustedRatings = AdjustedEloCalculator.FromBenchmark(
                team1Factory, team2Factory, result.Team1FinalElo, result.Team2FinalElo);
            if (adjustedRatings is not null
                && AnsiConsole.Confirm("Save adjusted ratings to database?", defaultValue: false))
            {
                var connectionString = settings.ConnectionString ?? ConnectionStringBuilder.FromEnvironment();
                await BotRatingUpdater.SaveAdjustedRatingsAsync(connectionString, adjustedRatings);
                AnsiConsole.MarkupLine("[green]Ratings saved to database.[/]");
            }

            return 0;
        }
        finally
        {
            (team1Factory as IDisposable)?.Dispose();
            (team2Factory as IDisposable)?.Dispose();
        }
    }
}
