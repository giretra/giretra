namespace Giretra.Core.Players.Agents.Remote;

/// <summary>
/// Metadata describing an external bot, deserialized from bot.meta.json.
/// </summary>
public sealed record BotMetadata
{
    public Guid? Id { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? AgentName { get; init; }
    public string? Pun { get; init; }
    public string? Author { get; init; }
    public string? AuthorGithub { get; init; }
    public string[]? Notifications { get; init; }
    public BotInitConfig? Init { get; init; }
    public required BotLaunchConfig Launch { get; init; }
}

/// <summary>
/// Optional initialization command to run before launching the bot (e.g. npm install).
/// </summary>
public sealed record BotInitConfig
{
    public required string Command { get; init; }
    public string Arguments { get; init; } = "";
    public int Timeout { get; init; } = 120;
}

/// <summary>
/// Launch configuration for an external bot process.
/// </summary>
public sealed record BotLaunchConfig
{
    public required string FileName { get; init; }
    public string Arguments { get; init; } = "";
    public int? Port { get; init; }
    public int StartupTimeout { get; init; } = 30;
    public string HealthEndpoint { get; init; } = "health";
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
}
