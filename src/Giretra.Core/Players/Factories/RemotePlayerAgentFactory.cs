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
    private StringBuilder? _processStdout;
    private StringBuilder? _processStderr;
    private Task? _stdoutDrainTask;
    private Task? _stderrDrainTask;

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

            // On Windows, commands like "npm" and "mvn" are .cmd/.bat scripts
            // that require cmd /c to resolve.  Real executables (bash, python,
            // java, etc.) must NOT be wrapped because the extra cmd layer breaks
            // handle inheritance — nested cmd → bash → python -m venv fails
            // with [WinError 6] "Invalid handle".
            if (OperatingSystem.IsWindows() && !CanFindExecutableOnPath(init.Command))
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
                var errorMessage = new StringBuilder();
                errorMessage.Append(
                    $"Init command '{init.Command} {init.Arguments}' failed with exit code {initProcess.ExitCode}.");

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    errorMessage.AppendLine();
                    errorMessage.AppendLine("--- stdout ---");
                    errorMessage.Append(stdout.TrimEnd());
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    errorMessage.AppendLine();
                    errorMessage.AppendLine("--- stderr ---");
                    errorMessage.Append(stderr.TrimEnd());
                }

                throw new InvalidOperationException(errorMessage.ToString());
            }

        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _processConfig.FileName,
            Arguments = _processConfig.Arguments,
            UseShellExecute = false,
            // Redirect stdout/stderr and consume them asynchronously via
            // background drain tasks. This prevents the OS pipe buffer (~4KB)
            // from filling up while still capturing output for error reporting.
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

        // Drain stdout/stderr in background to prevent pipe buffer deadlock.
        // Keeps only the last 16KB so chatty processes don't consume memory.
        _processStdout = new StringBuilder();
        _processStderr = new StringBuilder();
        _stdoutDrainTask = DrainStreamAsync(_process.StandardOutput, _processStdout);
        _stderrDrainTask = DrainStreamAsync(_process.StandardError, _processStderr);

        // Poll health endpoint until healthy or timeout
        var healthUrl = _baseUrl.TrimEnd('/') + '/' + _processConfig.HealthEndpoint.TrimStart('/');
        using var healthClient = new HttpClient();
        var deadline = DateTime.UtcNow + _processConfig.StartupTimeout;
        var pollCount = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process.HasExited)
            {
                var exitOutput = await GetCapturedOutputAsync();
                throw new InvalidOperationException(
                    $"Bot process exited prematurely with code {_process.ExitCode}.{exitOutput}");
            }

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
        var timeoutOutput = await GetCapturedOutputAsync();
        KillProcess();
        throw new TimeoutException(
            $"Bot process did not become healthy within {_processConfig.StartupTimeout}.{timeoutOutput}");
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

    /// <summary>
    /// Waits for background drain tasks to finish, then formats the captured
    /// stdout/stderr from the bot process for inclusion in error messages.
    /// </summary>
    private async Task<string> GetCapturedOutputAsync()
    {
        try
        {
            await Task.WhenAll(
                _stdoutDrainTask ?? Task.CompletedTask,
                _stderrDrainTask ?? Task.CompletedTask
            ).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException) { /* Use whatever was captured so far */ }

        return FormatCapturedOutput();
    }

    private string FormatCapturedOutput()
    {
        var stdout = _processStdout?.ToString().TrimEnd() ?? "";
        var stderr = _processStderr?.ToString().TrimEnd() ?? "";

        if (string.IsNullOrEmpty(stdout) && string.IsNullOrEmpty(stderr))
            return "";

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout))
        {
            sb.AppendLine();
            sb.AppendLine("--- stdout ---");
            sb.Append(stdout);
        }
        if (!string.IsNullOrEmpty(stderr))
        {
            sb.AppendLine();
            sb.AppendLine("--- stderr ---");
            sb.Append(stderr);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reads a stream into a StringBuilder in the background, keeping only the
    /// last <paramref name="maxChars"/> characters to bound memory usage.
    /// </summary>
    private static async Task DrainStreamAsync(StreamReader reader, StringBuilder sb, int maxChars = 16384)
    {
        var buffer = new Memory<char>(new char[1024]);
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer)) > 0)
            {
                sb.Append(buffer.Span[..read]);
                if (sb.Length > maxChars)
                    sb.Remove(0, sb.Length - maxChars);
            }
        }
        catch
        {
            // Process killed or stream closed — expected during cleanup
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
    /// Checks whether <paramref name="command"/> resolves to a real executable
    /// (.exe or .com) on the system PATH.  Commands that are .cmd/.bat scripts
    /// (e.g. npm, mvn) will NOT be found and should be routed through cmd /c.
    /// </summary>
    private static bool CanFindExecutableOnPath(string command)
    {
        // If the command already has an executable extension, trust it.
        var ext = Path.GetExtension(command);
        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".com", StringComparison.OrdinalIgnoreCase))
            return true;

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            if (File.Exists(Path.Combine(dir, command + ".exe")) ||
                File.Exists(Path.Combine(dir, command + ".com")))
                return true;
        }

        return false;
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
