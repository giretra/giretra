using Giretra.Core.Players;

namespace Giretra.Benchmark.Factories;

/// <summary>
/// Factory that creates RandomPlayerAgent instances.
/// </summary>
public sealed class RandomPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "RandomPlayer";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new RandomPlayerAgent(position);
    }
}
