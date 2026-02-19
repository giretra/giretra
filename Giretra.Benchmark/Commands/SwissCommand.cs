using System.ComponentModel;
using Giretra.Benchmark.Discovery;
using Giretra.Benchmark.Output;
using Giretra.Benchmark.Swiss;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Giretra.Benchmark.Commands;

public sealed class SwissSettings : CommandSettings
{
    [CommandArgument(0, "[agents]")]
    [Description("Agent factory names to include (default: all discovered)")]
    public string[]? Agents { get; init; }

    [CommandOption("-r|--rounds")]
    [Description("Number of rounds to play")]
    [DefaultValue(1000)]
    public int Rounds { get; init; } = 1000;

    [CommandOption("-t|--target")]
    [Description("Target score to win a match")]
    [DefaultValue(1000)]
    public int TargetScore { get; init; } = 1000;

    [CommandOption("-s|--seed")]
    [Description("Random seed for reproducibility")]
    public int? Seed { get; init; }

    [CommandOption("--shuffle")]
    [Description("Shuffle deck each match (for deterministic agents)")]
    [DefaultValue(true)]
    public bool Shuffle { get; init; } = true;

    [CommandOption("--elo")]
    [Description("Initial ELO rating for all participants")]
    [DefaultValue(1200.0)]
    public double Elo { get; init; } = 1200.0;

    [CommandOption("-k|--k-max")]
    [Description("Maximum ELO K-factor (early rounds)")]
    [DefaultValue(40.0)]
    public double KFactorMax { get; init; } = 40;

    [CommandOption("--k-min")]
    [Description("Minimum ELO K-factor (late rounds)")]
    [DefaultValue(4.0)]
    public double KFactorMin { get; init; } = 4;

    [CommandOption("--k-half-life")]
    [Description("Matches until K-factor is halfway between max and min")]
    [DefaultValue(30.0)]
    public double KFactorHalfLife { get; init; } = 30;
}

public sealed class SwissCommand : AsyncCommand<SwissSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SwissSettings settings, CancellationToken cancellation)
    {
        var available = FactoryDiscovery.DiscoverAll();

        var factories = settings.Agents is { Length: > 0 }
            ? FactoryDiscovery.Resolve(settings.Agents, available)
            : available.Values.ToList();

        if (factories.Count < 2)
        {
            AnsiConsole.MarkupLine("[red]Error: At least 2 agent factories are required for a Swiss tournament.[/]");
            AnsiConsole.MarkupLine($"[dim]Available: {string.Join(", ", available.Keys.OrderBy(k => k))}[/]");
            return 1;
        }

        var config = new SwissConfig
        {
            Rounds = settings.Rounds,
            TargetScore = settings.TargetScore,
            Seed = settings.Seed,
            Shuffle = settings.Shuffle,
            InitialElo = settings.Elo,
            EloKFactorMax = settings.KFactorMax,
            EloKFactorMin = settings.KFactorMin,
            EloKFactorHalfLife = settings.KFactorHalfLife
        };

        var participantNames = factories.Select(f => f.DisplayName).ToList();
        var renderer = new SwissRenderer(config, participantNames);
        var runner = new SwissRunner(factories, config);

        renderer.RenderHeader();
        runner.OnRoundCompleted += renderer.RenderRoundResult;

        var result = await runner.RunAsync();

        renderer.RenderFinalRanking(result);
        renderer.RenderStatistics(result);
        renderer.RenderAdjustedElo(result);

        return 0;
    }
}
