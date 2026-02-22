using System.Security.Cryptography;
using System.Text;
using Giretra.Core.Players.Agents.Remote;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates player agents for locally-discovered external bots.
/// Wraps a <see cref="RemotePlayerAgentFactory"/> with metadata from bot.meta.json.
/// </summary>
public sealed class LocalWsPlayerAgentFactory : IPlayerAgentFactory, IDisposable
{
    private readonly RemotePlayerAgentFactory _inner;

    public Guid Identifier { get; }
    public string AgentName { get; }
    public string DisplayName { get; }
    public string Pun { get; }

    public LocalWsPlayerAgentFactory(BotMetadata metadata, string botDirectory)
    {
        var agentName = metadata.AgentName
            ?? metadata.Name
            ?? Path.GetFileName(botDirectory);

        AgentName = agentName;
        DisplayName = metadata.DisplayName ?? agentName;
        Pun = metadata.Pun ?? string.Empty;
        Identifier = metadata.Id ?? DeriveIdentifier(agentName);

        var baseUrl = $"http://localhost:{metadata.Launch.Port}";
        var envVars = new Dictionary<string, string>(
            metadata.Launch.EnvironmentVariables ?? []);
        envVars.TryAdd("PORT", metadata.Launch.Port.ToString());

        var processConfig = new BotProcessConfig
        {
            FileName = metadata.Launch.FileName,
            Arguments = metadata.Launch.Arguments,
            WorkingDirectory = botDirectory,
            EnvironmentVariables = envVars,
            StartupTimeout = TimeSpan.FromSeconds(metadata.Launch.StartupTimeout),
            HealthEndpoint = metadata.Launch.HealthEndpoint
        };

        HashSet<string>? enabledNotifications = metadata.Notifications is not null
            ? new HashSet<string>(metadata.Notifications, StringComparer.OrdinalIgnoreCase)
            : null;

        _inner = new RemotePlayerAgentFactory(
            baseUrl, agentName, DisplayName, Pun,
            processConfig: processConfig,
            enabledNotifications: enabledNotifications);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _inner.InitializeAsync(cancellationToken);

    public IPlayerAgent Create(PlayerPosition position)
        => _inner.Create(position);

    public void Dispose() => _inner.Dispose();

    /// <summary>
    /// Derives a stable GUID from the agent name so the same local bot
    /// always produces the same Identifier.
    /// </summary>
    private static Guid DeriveIdentifier(string agentName)
    {
        var input = $"LocalBot:{agentName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
