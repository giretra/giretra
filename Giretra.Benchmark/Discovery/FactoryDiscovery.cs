using System.Reflection;
using Giretra.Core.Players;

namespace Giretra.Benchmark.Discovery;

/// <summary>
/// Discovers IPlayerAgentFactory implementations via reflection.
/// </summary>
public static class FactoryDiscovery
{
    /// <summary>
    /// Discovers all concrete IPlayerAgentFactory implementations in Giretra.Core.
    /// </summary>
    public static Dictionary<string, IPlayerAgentFactory> DiscoverAll()
    {
        var assembly = typeof(IPlayerAgentFactory).Assembly;
        var factoryType = typeof(IPlayerAgentFactory);

        var factories = new Dictionary<string, IPlayerAgentFactory>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !factoryType.IsAssignableFrom(type))
                continue;

            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? type.GetConstructors().FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));

            if (constructor is null)
                continue;

            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            var factory = (IPlayerAgentFactory)constructor.Invoke(args);
            factories[factory.AgentName] = factory;
        }

        return factories;
    }

    /// <summary>
    /// Resolves factory names to instances, validating that all names are known.
    /// </summary>
    public static List<IPlayerAgentFactory> Resolve(IEnumerable<string> names, Dictionary<string, IPlayerAgentFactory> available)
    {
        var result = new List<IPlayerAgentFactory>();

        foreach (var name in names)
        {
            if (!available.TryGetValue(name, out var factory))
            {
                var known = string.Join(", ", available.Keys.OrderBy(k => k));
                throw new ArgumentException($"Unknown agent '{name}'. Available: {known}");
            }

            result.Add(factory);
        }

        return result;
    }
}
