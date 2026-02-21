namespace Giretra.Core.Players.Agents.Remote;

/// <summary>
/// Configuration for launching and health-checking a bot process.
/// </summary>
public sealed record BotProcessConfig
{
    /// <summary>
    /// Path to the executable to launch.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Command-line arguments passed to the executable.
    /// </summary>
    public string Arguments { get; init; } = "";

    /// <summary>
    /// Working directory for the process. Inherits from the current process if null.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Extra environment variables to set on the process.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Maximum time to wait for the health check to succeed before giving up.
    /// </summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Interval between health check polls.
    /// </summary>
    public TimeSpan StartupPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Relative path appended to the base URL for the health check endpoint.
    /// </summary>
    public string HealthEndpoint { get; init; } = "health";
}
