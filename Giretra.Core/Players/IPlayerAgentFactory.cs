namespace Giretra.Core.Players;

/// <summary>
/// Factory interface for creating player agents.
/// </summary>
public interface IPlayerAgentFactory
{
    /// <summary>
    /// Gets the name of the agent type this factory creates.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Gets a user-friendly display name for the agent type.
    /// </summary>
    string DisplayName => AgentName;

    /// <summary>
    /// Creates a new player agent for the specified position.
    /// </summary>
    IPlayerAgent Create(PlayerPosition position);
}
