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
    private readonly IReadOnlySet<string>? _enabledNotifications;
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
        BotProcessConfig? processConfig = null,
        IReadOnlySet<string>? enabledNotifications = null)
    {
        AgentName = agentName;
        DisplayName = displayName ?? agentName;
        Pun = pun ?? string.Empty;
        Identifier = DeriveIdentifier(baseUrl, agentName);
        _matchId = Guid.NewGuid().ToString();
        _baseUrl = baseUrl;
        _processConfig = processConfig;
        _enabledNotifications = enabledNotifications;

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

        // Run init command if configured (e.g. npm install)
        if (_processConfig.Init is { } init)
        {
            var initStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // On Windows, commands like "npm" are .cmd scripts that require
            // the shell to resolve. Route through cmd /c so they are found.
            if (OperatingSystem.IsWindows())
            {
                initStartInfo.FileName = "cmd";
                initStartInfo.Arguments = string.IsNullOrEmpty(init.Arguments)
                    ? $"/c {init.Command}"
                    : $"/c {init.Command} {init.Arguments}";
            }
            else
            {
                initStartInfo.FileName = init.Command;
                initStartInfo.Arguments = init.Arguments;
            }

            if (_processConfig.WorkingDirectory is not null)
                initStartInfo.WorkingDirectory = _processConfig.WorkingDirectory;

            using var initProcess = Process.Start(initStartInfo)
                ?? throw new InvalidOperationException(
                    $"Failed to start init command: {init.Command} {init.Arguments}");

            using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            initCts.CancelAfter(TimeSpan.FromSeconds(init.Timeout));

            try
            {
                await initProcess.WaitForExitAsync(initCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                initProcess.Kill(entireProcessTree: true);
                throw new TimeoutException(
                    $"Init command '{init.Command} {init.Arguments}' timed out after {init.Timeout}s.");
            }

            if (initProcess.ExitCode != 0)
            {
                var stderr = await initProcess.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Init command '{init.Command} {init.Arguments}' failed with exit code {initProcess.ExitCode}. " +
                    $"stderr: {stderr}");
            }
        }

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
        return new RemotePlayerAgent(_client, position, _matchId, _enabledNotifications);
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
