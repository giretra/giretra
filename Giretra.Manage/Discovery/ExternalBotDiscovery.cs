using System.Text.Json;
using Giretra.Core.Players;
using Giretra.Core.Players.Agents.Remote;
using Giretra.Core.Players.Factories;
using Spectre.Console;

namespace Giretra.Manage.Discovery;

/// <summary>
/// Scans the external-bots/ directory for bot subdirectories containing bot.meta.json
/// and creates <see cref="LocalWsPlayerAgentFactory"/> instances for each valid bot.
/// </summary>
public static class ExternalBotDiscovery
{
    private const string MetadataFileName = "bot.meta.json";
    private const string ExternalBotsDirectory = "external-bots";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Walks up from the current directory to find the repository root (.git directory).
    /// Returns null if not inside a git repository.
    /// </summary>
    public static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Discovers external bots from the external-bots/ directory under <paramref name="basePath"/>.
    /// </summary>
    /// <param name="basePath">Repository root path containing the external-bots/ directory.</param>
    /// <param name="builtInNames">Names of built-in agents, used for collision warnings.</param>
    /// <returns>Dictionary of agent name to factory, empty if no external-bots/ directory exists.</returns>
    public static Dictionary<string, IPlayerAgentFactory> Discover(
        string basePath, IReadOnlyCollection<string>? builtInNames = null)
    {
        var externalBotsDir = Path.Combine(basePath, ExternalBotsDirectory);
        if (!Directory.Exists(externalBotsDir))
            return new Dictionary<string, IPlayerAgentFactory>(StringComparer.OrdinalIgnoreCase);

        var factories = new Dictionary<string, IPlayerAgentFactory>(StringComparer.OrdinalIgnoreCase);

        foreach (var botDir in Directory.GetDirectories(externalBotsDir))
        {
            var metadataPath = Path.Combine(botDir, MetadataFileName);
            if (!File.Exists(metadataPath))
                continue;

            BotMetadata? metadata;
            try
            {
                var json = File.ReadAllText(metadataPath);
                metadata = JsonSerializer.Deserialize<BotMetadata>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning:[/] Skipping [bold]{Path.GetFileName(botDir)}[/]: " +
                    $"malformed {MetadataFileName} ({ex.Message})");
                continue;
            }

            if (metadata?.Launch is null)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning:[/] Skipping [bold]{Path.GetFileName(botDir)}[/]: " +
                    $"missing 'launch' section in {MetadataFileName}");
                continue;
            }

            var agentName = metadata.AgentName ?? metadata.Name ?? Path.GetFileName(botDir);

            // Warn on name collision with built-in agents
            if (builtInNames?.Contains(agentName, StringComparer.OrdinalIgnoreCase) == true)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning:[/] External bot [bold]{agentName}[/] " +
                    $"overrides a built-in agent of the same name.");
            }

            var factory = new LocalWsPlayerAgentFactory(metadata, botDir);
            factories[factory.AgentName] = factory;
        }

        return factories;
    }
}
