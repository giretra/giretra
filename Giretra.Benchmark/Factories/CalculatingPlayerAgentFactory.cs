using Giretra.Core.Players;

namespace Giretra.Benchmark.Factories;

/// <summary>
/// Factory that creates CalculatingPlayerAgent instances.
/// </summary>
public sealed class CalculatingPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "CalculatingPlayer";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new CalculatingPlayerAgent(position);
    }
}
