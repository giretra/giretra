using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates CalculatingPlayerAgent instances.
/// </summary>
public sealed class CalculatingPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "CalculatingPlayer";
    public string DisplayName => "Razazavavy";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new CalculatingPlayerAgent(position);
    }
}
