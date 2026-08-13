using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates CuttingPlayerAgent instances.
/// </summary>
public sealed class CuttingPlayerAgentFactory : IPlayerAgentFactory
{
    public Guid Identifier { get; } = Guid.Parse("95f3d980-c96c-4df2-9c3f-faa8bab8dd8e");

    public string AgentName => "CuttingPlayer";

    public string DisplayName => "Baomijijy";

    public string Pun => "Ny azy ny safidy, vita hatrany am-piandohana.";

    public IPlayerAgent Create(PlayerPosition position)
    {
        return new CuttingPlayerAgent(position);
    }
}
