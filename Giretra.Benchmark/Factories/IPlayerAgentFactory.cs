using Giretra.Core.Players;

namespace Giretra.Benchmark.Factories;

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
    /// Creates a new player agent for the specified position.
    /// </summary>
    IPlayerAgent Create(PlayerPosition position);
}
