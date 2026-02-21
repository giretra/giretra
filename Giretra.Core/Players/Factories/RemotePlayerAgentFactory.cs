using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Giretra.Core.Players.Agents.Remote;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates RemotePlayerAgent instances connected to a remote bot HTTP server.
/// Each agent gets its own session on the remote server.
/// The factory owns the shared RemoteBotClient (and optionally the HttpClient).
/// Optionally launches and manages the bot process lifecycle.
/// </summary>
public sealed class RemotePlayerAgentFactory : IPlayerAgentFactory, IDisposable
{
    private readonly RemoteBotClient _client;
    private readonly string _baseUrl;
    private readonly string _matchId;
    private readonly BotProcessConfig? _processConfig;
    private Process? _process;

    public Guid Identifier { get; }
    public string AgentName { get; }
    public string DisplayName { get; }
    public string Pun { get; }

    /// <summary>
    /// Creates a factory that connects to a remote bot server.
    /// </summary>
    /// <param name="baseUrl">Base URL of the remote bot server (e.g. "https://bot.example.com").</param>
    /// <param name="agentName">Internal agent name.</param>
    /// <param name="displayName">User-friendly display name (defaults to agentName).</param>
    /// <param name="pun">Optional tagline.</param>
    /// <param name="httpClient">Optional pre-configured HttpClient. If null, one is created internally.</param>
    /// <param name="decisionTimeout">Timeout for decision endpoints (default 30s).</param>
    /// <param name="notificationTimeout">Timeout for notification endpoints (default 5s).</param>
    /// <param name="processConfig">Optional config to launch the bot process automatically.</param>
    public RemotePlayerAgentFactory(
        string baseUrl,
        string agentName,
        string? displayName = null,
        string? pun = null,
        HttpClient? httpClient = null,
        TimeSpan? decisionTimeout = null,
        TimeSpan? notificationTimeout = null,
        BotProcessConfig? processConfig = null)
    {
        AgentName = agentName;
        DisplayName = displayName ?? agentName;
        Pun = pun ?? string.Empty;
        Identifier = DeriveIdentifier(baseUrl, agentName);
        _matchId = Guid.NewGuid().ToString();
        _baseUrl = baseUrl;
        _processConfig = processConfig;

        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient();
        httpClient.BaseAddress ??= new Uri(baseUrl.TrimEnd('/') + '/');

        _client = new RemoteBotClient(httpClient, ownsClient, decisionTimeout, notificationTimeout);
    }

    /// <summary>
    /// If a <see cref="BotProcessConfig"/> was provided, launches the bot process and
    /// polls the health endpoint until it responds successfully or the timeout expires.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_processConfig is null)
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = _processConfig.FileName,
            Arguments = _processConfig.Arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (_processConfig.WorkingDirectory is not null)
            startInfo.WorkingDirectory = _processConfig.WorkingDirectory;

        foreach (var (key, value) in _processConfig.EnvironmentVariables)
            startInfo.Environment[key] = value;

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Failed to start bot process: {_processConfig.FileName}");

        // Poll health endpoint until healthy or timeout
        var healthUrl = _baseUrl.TrimEnd('/') + '/' + _processConfig.HealthEndpoint.TrimStart('/');
        using var healthClient = new HttpClient();
        var deadline = DateTime.UtcNow + _processConfig.StartupTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"Bot process exited prematurely with code {_process.ExitCode}.");

            try
            {
                using var response = await healthClient.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet — retry
            }

            await Task.Delay(_processConfig.StartupPollInterval, cancellationToken);
        }

        // Timeout — kill the process and throw
        KillProcess();
        throw new TimeoutException(
            $"Bot process did not become healthy within {_processConfig.StartupTimeout}.");
    }

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new RemotePlayerAgent(_client, position, _matchId);
    }

    public void Dispose()
    {
        _client.Dispose();
        KillProcess();
    }

    private void KillProcess()
    {
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Derives a stable GUID from the base URL and agent name so the same
    /// remote bot configuration always produces the same Identifier.
    /// </summary>
    private static Guid DeriveIdentifier(string baseUrl, string agentName)
    {
        var input = $"RemoteBot:{baseUrl}:{agentName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Take the first 16 bytes of the SHA-256 hash to form a GUID
        return new Guid(hash.AsSpan(0, 16));
    }
}
