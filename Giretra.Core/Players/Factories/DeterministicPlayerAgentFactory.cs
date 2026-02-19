using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates DeterministicPlayerAgent instances.
/// </summary>
public sealed class DeterministicPlayerAgentFactory : IPlayerAgentFactory
{
    public Guid Identifier { get; } = Guid.Parse("e3b45a6a-6c43-b80e-5281-beb17a9c8151");

    public string AgentName => "DeterministicPlayer";

    public string DisplayName => "Eva";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new DeterministicPlayerAgent(position);
    }
}
