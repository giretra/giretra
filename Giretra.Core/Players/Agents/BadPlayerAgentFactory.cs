namespace Giretra.Core.Players.Agents;

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
