using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates CalculatingPlayerAgent instances.
/// </summary>
public sealed class CalculatingPlayerAgentFactory : IPlayerAgentFactory
{
    public Guid Identifier { get; } = Guid.Parse("ac393249-03a2-8575-a91f-ea9f9ab7687c");

    public string AgentName => "CalculatingPlayer";
    public string DisplayName => "Razazavavy";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new CalculatingPlayerAgent(position);
    }
}
