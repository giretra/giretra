using System.ComponentModel;
using Giretra.Core.Players;
using Giretra.Core.Players.Discovery;
using Giretra.Core.Players.Factories;
using Giretra.Manage.Analysis;
using Giretra.Manage.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Giretra.Manage.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandArgument(0, "[team1]")]
    [Description("Team 1 agent by AgentName or DisplayName (default: DeterministicPlayer)")]
    public string? Team1 { get; init; }

    [CommandArgument(1, "[team2]")]
    [Description("Team 2 agent by AgentName or DisplayName (default: CalculatingPlayer)")]
    public string? Team2 { get; init; }

    [CommandOption("-n|--matches")]
    [Description("Number of matches to play")]
    [DefaultValue(20)]
    public int MatchCount { get; init; } = 20;

    [CommandOption("-t|--target")]
    [Description("Target score to win a match")]
    [DefaultValue(150)]
    public int TargetScore { get; init; } = 150;

    [CommandOption("-s|--seed")]
    [Description("Random seed for reproducibility")]
    public int? Seed { get; init; }

    [CommandOption("-w|--worst")]
    [Description("Number of costliest mistakes to list")]
    [DefaultValue(10)]
    public int Worst { get; init; } = 10;
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings, CancellationToken cancellation)
    {
        IPlayerAgentFactory team1Factory;
        IPlayerAgentFactory team2Factory;

        if (settings.Team1 is not null || settings.Team2 is not null)
        {
            var available = FactoryDiscovery.DiscoverAll(msg => AnsiConsole.MarkupLine($"[yellow]{msg}[/]"));
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

        var runner = new AnalysisRunner(team1Factory, team2Factory, settings.MatchCount, settings.TargetScore, settings.Seed);

        try
        {
            await runner.InitializeAsync(cancellation);

            AnalysisRenderer.RenderHeader(team1Factory.DisplayName, team2Factory.DisplayName,
                settings.MatchCount, settings.TargetScore, settings.Seed);

            var result = await AnsiConsole.Progress().StartAsync(async progress =>
            {
                var playing = progress.AddTask("Playing matches", maxValue: settings.MatchCount);
                var solving = progress.AddTask("Solving deals", autoStart: false);

                runner.OnMatchPlayed += played => playing.Value = played;
                runner.OnDealAnalyzed += (done, total) =>
                {
                    solving.MaxValue = total;
                    solving.StartTask();
                    solving.Value = done;
                };

                return await runner.RunAsync(cancellation);
            });

            AnalysisRenderer.RenderSummary(result, settings.Worst);
            return 0;
        }
        finally
        {
            (team1Factory as IDisposable)?.Dispose();
            (team2Factory as IDisposable)?.Dispose();
        }
    }
}
