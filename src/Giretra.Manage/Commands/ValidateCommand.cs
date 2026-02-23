using System.ComponentModel;
using Giretra.Manage.Discovery;
using Giretra.Manage.Output;
using Giretra.Manage.Validation;
using Giretra.Core.Players.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using ValidationResult = Giretra.Manage.Validation.ValidationResult;

namespace Giretra.Manage.Commands;

public sealed class ValidateSettings : CommandSettings
{
    [CommandArgument(0, "<agent>")]
    [Description("Bot to validate by AgentName or DisplayName")]
    public string Agent { get; init; } = null!;

    [CommandOption("-n|--matches")]
    [Description("Number of matches to play")]
    [DefaultValue(100)]
    public int MatchCount { get; init; } = 100;

    [CommandOption("-t|--target")]
    [Description("Target score to win a match")]
    [DefaultValue(151)]
    public int TargetScore { get; init; } = 151;

    [CommandOption("-s|--seed")]
    [Description("Random seed for reproducibility")]
    public int? Seed { get; init; }

    [CommandOption("--shuffle")]
    [Description("Shuffle deck each match")]
    [DefaultValue(true)]
    public bool Shuffle { get; init; } = true;

    [CommandOption("-o|--opponent")]
    [Description("Opponent agent by AgentName or DisplayName (default: RandomPlayer)")]
    public string? Opponent { get; init; }

    [CommandOption("--timeout")]
    [Description("Max response time in ms (violation if exceeded)")]
    public int? Timeout { get; init; }

    [CommandOption("-d|--determinism")]
    [Description("Run twice with same seed and verify identical decisions")]
    [DefaultValue(false)]
    public bool Determinism { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Show every violation in detail")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }
}

public sealed class ValidateCommand : AsyncCommand<ValidateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ValidateSettings settings, CancellationToken cancellation)
    {
        var config = new ValidationConfig
        {
            MatchCount = settings.MatchCount,
            TargetScore = settings.TargetScore,
            Seed = settings.Seed,
            Shuffle = settings.Shuffle,
            TimeoutMs = settings.Timeout,
            Determinism = settings.Determinism,
            Verbose = settings.Verbose
        };

        var available = FactoryDiscovery.DiscoverAll();
        var agentFactory = FactoryDiscovery.Resolve([settings.Agent], available)[0];

        var opponentFactory = settings.Opponent is not null
            ? FactoryDiscovery.Resolve([settings.Opponent], available)[0]
            : new RandomPlayerAgentFactory();

        var runner = new ValidationRunner(agentFactory, opponentFactory, config);
        var renderer = new ValidationRenderer(config, agentFactory.DisplayName, opponentFactory.DisplayName);

        try
        {
            await runner.InitializeAsync(cancellation);

            renderer.RenderHeader();
            runner.OnMatchCompleted += renderer.RenderMatchResult;

            var result = await runner.RunAsync();

            // Determinism check if requested
            if (config.Determinism)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Running determinism check (replaying with same seed)...[/]");
                var determinismViolations = await runner.RunDeterminismCheckAsync();

                // Merge determinism violations into result
                result = new ValidationResult
                {
                    MatchesCompleted = result.MatchesCompleted,
                    MatchesCrashed = result.MatchesCrashed,
                    TotalDeals = result.TotalDeals,
                    Violations = result.Violations,
                    TimingStats = result.TimingStats,
                    GameModesCovered = result.GameModesCovered,
                    AgentTeamWins = result.AgentTeamWins,
                    OpponentTeamWins = result.OpponentTeamWins,
                    CrashDetails = result.CrashDetails,
                    TotalDuration = result.TotalDuration,
                    NotificationWarnings = result.NotificationWarnings,
                    DeterminismViolations = determinismViolations,
                    PerformanceTrend = result.PerformanceTrend
                };
            }

            renderer.RenderSummary(result);

            return result.HasViolations ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold red]Infrastructure error ({Markup.Escape(ex.GetType().Name)}):[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(ex.Message);
            return 2;
        }
        finally
        {
            (agentFactory as IDisposable)?.Dispose();
            (opponentFactory as IDisposable)?.Dispose();
        }
    }
}
