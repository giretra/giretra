using Giretra.Core.Players;

namespace Giretra.Benchmark.Factories;

/// <summary>
/// Factory that creates BadPlayerAgent instances.
/// </summary>
public sealed class BadPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "BadPlayer";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new BadPlayerAgent(position);
    }
}
