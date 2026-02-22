namespace Giretra.Core.Players;

/// <summary>
/// Factory interface for creating player agents.
/// </summary>
public interface IPlayerAgentFactory
{
    /// <summary>
    /// Gets the unique identifier for this agent type.
    /// </summary>
    Guid Identifier { get; }

    /// <summary>
    /// Gets the name of the agent type this factory creates.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Gets a user-friendly display name for the agent type.
    /// </summary>
    string DisplayName => AgentName;

    /// <summary>
    /// Gets a short pun or tagline for this agent type.
    /// </summary>
    string Pun => string.Empty;

    /// <summary>
    /// Performs any async initialization required before the factory can create agents
    /// (e.g. launching an external bot process and waiting for it to become healthy).
    /// The default implementation is a no-op.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Creates a new player agent for the specified position.
    /// </summary>
    IPlayerAgent Create(PlayerPosition position);
}
