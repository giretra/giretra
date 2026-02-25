using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates BadPlayerAgent instances.
/// </summary>
public sealed class BadPlayerAgentFactory : IPlayerAgentFactory
{
    public Guid Identifier { get; } = Guid.Parse("d61b4c0e-13f0-3929-752c-ddb135be94c8");

    public string AgentName => "BadPlayer";
    
    public string DisplayName => "Masaymasay";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new BadPlayerAgent(position);
    }
}
