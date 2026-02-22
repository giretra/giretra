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

        var port = metadata.Launch.Port ?? PortAllocator.AllocateFreePort();
        var baseUrl = $"http://localhost:{port}";
        var envVars = new Dictionary<string, string>(
            metadata.Launch.EnvironmentVariables ?? []);
        envVars.TryAdd("PORT", port.ToString());

        var processConfig = new BotProcessConfig
        {
            FileName = metadata.Launch.FileName,
            Arguments = metadata.Launch.Arguments,
            WorkingDirectory = botDirectory,
            EnvironmentVariables = envVars,
            StartupTimeout = TimeSpan.FromSeconds(metadata.Launch.StartupTimeout),
            HealthEndpoint = metadata.Launch.HealthEndpoint,
            Init = metadata.Init
        };

        HashSet<string>? enabledNotifications = metadata.Notifications is not null
            ? new HashSet<string>(metadata.Notifications, StringComparer.OrdinalIgnoreCase)
            : null;

        _inner = new RemotePlayerAgentFactory(
            baseUrl, agentName, DisplayName, Pun,
            processConfig: processConfig,
            enabledNotifications: enabledNotifications);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
        => await _inner.InitializeAsync(cancellationToken);

    /// <summary>
    /// Returns a short diagnostic string about the bot process status.
    /// </summary>
    public string ProcessStatus => _inner.ProcessStatus;

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
