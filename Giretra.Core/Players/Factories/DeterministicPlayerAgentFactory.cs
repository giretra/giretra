using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates DeterministicPlayerAgent instances.
/// </summary>
public sealed class DeterministicPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "DeterministicPlayer";
    public string DisplayName => "Deterministic";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new DeterministicPlayerAgent(position);
    }
}
