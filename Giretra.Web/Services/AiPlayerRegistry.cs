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
    /// Gets the list of available AI types with display names.
    /// </summary>
    public IReadOnlyList<AiTypeInfo> GetAvailableTypes() =>
        _factories.Values.Select(f => new AiTypeInfo(f.AgentName, f.DisplayName)).ToList();

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

    public string GetDisplayName(string aiType) =>
        _factories.TryGetValue(aiType, out var factory) ? factory.DisplayName : aiType;
}

public record AiTypeInfo(string Name, string DisplayName);
