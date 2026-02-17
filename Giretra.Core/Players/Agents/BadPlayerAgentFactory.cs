using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates BadPlayerAgent instances.
/// </summary>
public sealed class BadPlayerAgentFactory : IPlayerAgentFactory
{
    public string AgentName => "BadPlayer";
    public string DisplayName => "Beginner";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new BadPlayerAgent(position);
    }
}
