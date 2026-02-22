using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Returns a short diagnostic string about the bot process status.
    /// </summary>
    public string ProcessStatus
    {
        get
        {
            if (_process is null) return "no process (remote-only)";
            try
            {
                return _process.HasExited
                    ? $"EXITED (code {_process.ExitCode}, PID {_process.Id})"
                    : $"running (PID {_process.Id})";
            }
            catch (InvalidOperationException)
            {
                return "unknown (disposed)";
            }
        }
    }

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

            // Read stdout and stderr concurrently to prevent pipe buffer deadlock.
            // If only WaitForExitAsync is awaited while both streams are redirected,
            // the child process blocks once the OS pipe buffer (~4KB) fills up.
            var stdoutTask = initProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = initProcess.StandardError.ReadToEndAsync(cancellationToken);

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

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (initProcess.ExitCode != 0)
            {
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
            // Do NOT redirect stdout/stderr for long-running bot processes.
            // The streams would never be consumed, causing the OS pipe buffer
            // (~4KB) to fill up, which blocks the child process on write and
            // freezes its HTTP server ("response ended prematurely").
            RedirectStandardOutput = false,
            RedirectStandardError = false,
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
        var pollCount = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"Bot process exited prematurely with code {_process.ExitCode}.");

            pollCount++;
            try
            {
                using var response = await healthClient.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await VerifyPostEndpointAsync(healthClient, cancellationToken);
                    return;
                }

            }
            catch (HttpRequestException)
            {
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

    /// <summary>
    /// Smoke test: creates and immediately destroys a session to verify
    /// the bot handles POST requests correctly after health check passes.
    /// </summary>
    private async Task VerifyPostEndpointAsync(HttpClient healthClient, CancellationToken cancellationToken)
    {
        var sessionsUrl = _baseUrl.TrimEnd('/') + "/api/sessions";
        try
        {
            var json = """{"position":"Bottom","matchId":"__smoke_test__"}""";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await healthClient.PostAsync(sessionsUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Clean up the smoke test session
                try
                {
                    var sessionId = JsonDocument.Parse(body).RootElement.GetProperty("sessionId").GetString();
                    if (sessionId is not null)
                        await healthClient.DeleteAsync($"{_baseUrl.TrimEnd('/')}/api/sessions/{sessionId}", cancellationToken);
                }
                catch { /* best effort cleanup */ }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Bot process started and health check passed, but POST /api/sessions failed: {ex.Message}", ex);
        }
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
