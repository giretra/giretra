namespace Giretra.Core.Players.Discovery;

/// <summary>
/// Discovers IPlayerAgentFactory implementations via reflection and external bot directories.
/// </summary>
public static class FactoryDiscovery
{
    /// <summary>
    /// Discovers all concrete IPlayerAgentFactory implementations in Giretra.Core,
    /// then merges any external bots found in the external-bots/ directory.
    /// External bot discovery uses the current working directory by default.
    /// </summary>
    public static Dictionary<string, IPlayerAgentFactory> DiscoverAll(Action<string>? onWarning = null)
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

        var repoRoot = ExternalBotDiscovery.FindRepoRoot();
        if (repoRoot is not null)
        {
            var external = ExternalBotDiscovery.Discover(repoRoot, factories.Keys, onWarning);
            foreach (var (name, factory) in external)
                factories[name] = factory;
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
                // Fall back to matching by DisplayName
                factory = available.Values.FirstOrDefault(f =>
                    string.Equals(f.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            }

            if (factory is null)
            {
                var known = string.Join(", ", available.Values
                    .OrderBy(f => f.AgentName)
                    .Select(f => f.AgentName == f.DisplayName
                        ? f.AgentName
                        : $"{f.AgentName} ({f.DisplayName})"));
                throw new ArgumentException($"Unknown agent '{name}'. Available: {known}");
            }

            result.Add(factory);
        }

        return result;
    }
}
