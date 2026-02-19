using System.ComponentModel;
using Giretra.Benchmark.Benchmarking;
using Giretra.Benchmark.Output;
using Giretra.Core.Players.Factories;
using Spectre.Console.Cli;

namespace Giretra.Benchmark.Commands;

public sealed class BenchmarkSettings : CommandSettings
{
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

        var team1Factory = new DeterministicPlayerAgentFactory();
        var team2Factory = new CalculatingPlayerAgentFactory();

        var runner = new BenchmarkRunner(team1Factory, team2Factory, config);
        var renderer = new BenchmarkRenderer(config, team1Factory.AgentName, team2Factory.AgentName);

        renderer.RenderHeader();
        runner.OnMatchCompleted += renderer.RenderMatchResult;

        var result = await runner.RunAsync();
        renderer.RenderSummary(result, team1Factory.AgentName, team2Factory.AgentName);

        return 0;
    }
}
