using Giretra.Core.Players;
using Giretra.Core.Players.Factories;

namespace Giretra.Web.Services;

/// <summary>
/// Registry that discovers and manages available AI player agent factories.
/// </summary>
public sealed class AiPlayerRegistry
{
    private readonly Dictionary<string, IPlayerAgentFactory> _factories;

    public AiPlayerRegistry()
    {
        var factories = new IPlayerAgentFactory[]
        {
            new CalculatingPlayerAgentFactory(),
            new RandomPlayerAgentFactory(),
            new DeterministicPlayerAgentFactory(),
            new BadPlayerAgentFactory()
        };

        _factories = factories.ToDictionary(f => f.AgentName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the list of available AI type names.
    /// </summary>
    public IReadOnlyList<string> GetAvailableTypes() => _factories.Keys.ToList();

    /// <summary>
    /// Creates an AI player agent of the specified type for the given position.
    /// Falls back to CalculatingPlayer if the type is not found.
    /// </summary>
    public IPlayerAgent CreateAgent(string aiType, PlayerPosition position)
    {
        if (_factories.TryGetValue(aiType, out var factory))
        {
            return factory.Create(position);
        }

        // Fallback to CalculatingPlayer
        return _factories["CalculatingPlayer"].Create(position);
    }
}
