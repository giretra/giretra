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
    public required BotLaunchConfig Launch { get; init; }
}

/// <summary>
/// Launch configuration for an external bot process.
/// </summary>
public sealed record BotLaunchConfig
{
    public required string FileName { get; init; }
    public string Arguments { get; init; } = "";
    public required int Port { get; init; }
    public int StartupTimeout { get; init; } = 30;
    public string HealthEndpoint { get; init; } = "health";
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
}
