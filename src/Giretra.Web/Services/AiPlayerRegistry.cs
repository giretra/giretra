using Giretra.Core.Players;
using Giretra.Core.Players.Discovery;
using Giretra.Core.Players.Factories;
using Giretra.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Giretra.Web.Services;

/// <summary>
/// Registry that loads available AI player agent factories from the database.
/// Requires sync-bots to have been run to populate the Bots table.
/// </summary>
public sealed class AiPlayerRegistry : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiPlayerRegistry> _logger;
    private Dictionary<string, CachedBot> _bots = new(StringComparer.OrdinalIgnoreCase);
    private string? _defaultAgentType;

    public AiPlayerRegistry(IServiceScopeFactory scopeFactory, ILogger<AiPlayerRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a registry pre-populated from all known factories via reflection (for testing).
    /// </summary>
    public static AiPlayerRegistry CreateFromAssembly()
    {
        var registry = new AiPlayerRegistry(null!, NullLogger<AiPlayerRegistry>.Instance);

        var factoryAssembly = typeof(IPlayerAgentFactory).Assembly;
        var factoryType = typeof(IPlayerAgentFactory);
        short difficulty = 0;

        foreach (var type in factoryAssembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !factoryType.IsAssignableFrom(type))
                continue;

            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? type.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));

            if (constructor is null)
                continue;

            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            var factory = (IPlayerAgentFactory)constructor.Invoke(args);

            registry._bots[factory.AgentName] = new CachedBot(
                factory.AgentName, factory.DisplayName, difficulty++, 1000,
                null, null, null, factory);
        }

        registry._defaultAgentType = registry._bots.Values
            .MaxBy(b => b.Difficulty)?.AgentType;

        return registry;
    }

    /// <summary>
    /// Loads active bots from the database and resolves their factories via reflection.
    /// Then calls InitializeAsync on each factory (e.g. to launch remote bot processes).
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiretraDbContext>();

        var bots = await db.Bots
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.Difficulty)
            .ToListAsync();

        var factoryAssembly = typeof(IPlayerAgentFactory).Assembly;
        var newBots = new Dictionary<string, CachedBot>(StringComparer.OrdinalIgnoreCase);

        foreach (var bot in bots)
        {
            var factoryType = factoryAssembly.GetType(bot.AgentTypeFactory)
                ?? Type.GetType(bot.AgentTypeFactory);

            if (factoryType is null)
            {
                _logger.LogWarning("Could not resolve factory type {FactoryType} for bot {BotName}",
                    bot.AgentTypeFactory, bot.AgentType);
                continue;
            }

            var constructor = factoryType.GetConstructor(Type.EmptyTypes)
                ?? factoryType.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));

            if (constructor is null)
            {
                _logger.LogWarning("No suitable constructor for factory {FactoryType}", bot.AgentTypeFactory);
                continue;
            }

            var args = constructor.GetParameters().Select(p => p.DefaultValue).ToArray();
            var factory = (IPlayerAgentFactory)constructor.Invoke(args);

            newBots[bot.AgentType] = new CachedBot(
                bot.AgentType,
                bot.DisplayName,
                bot.Difficulty,
                bot.Rating,
                bot.Pun,
                bot.Description,
                bot.Author,
                factory);
        }

        // Initialize each factory (e.g. launch remote bot processes)
        foreach (var (agentType, cached) in newBots)
        {
            try
            {
                await cached.Factory.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize factory for bot {AgentType}, skipping", agentType);
                if (cached.Factory is IDisposable disposable)
                    disposable.Dispose();
                newBots.Remove(agentType);
            }
        }

        _bots = newBots;
        _defaultAgentType = bots.FirstOrDefault()?.AgentType;

        _logger.LogInformation("Loaded {Count} active bot(s) from database", newBots.Count);
    }

    /// <summary>
    /// Discovers and initializes all available bot factories via reflection and external-bots/ directory.
    /// Used in offline mode (no database).
    /// </summary>
    public async Task InitializeOfflineAsync(CancellationToken cancellationToken = default)
    {
        var discovered = FactoryDiscovery.DiscoverAll(
            msg => _logger.LogWarning("{Message}", msg));

        var newBots = new Dictionary<string, CachedBot>(StringComparer.OrdinalIgnoreCase);
        short difficulty = 0;

        foreach (var (name, factory) in discovered)
        {
            try
            {
                await factory.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize factory for bot {AgentType}, skipping", name);
                if (factory is IDisposable disposable)
                    disposable.Dispose();
                continue;
            }

            newBots[name] = new CachedBot(
                name, factory.DisplayName, difficulty++, 1000,
                null, null, null, factory);
        }

        _bots = newBots;
        _defaultAgentType = newBots.Values.MaxBy(b => b.Difficulty)?.AgentType;

        _logger.LogInformation("Loaded {Count} bot(s) via offline discovery", newBots.Count);
    }

    /// <summary>
    /// Gets the list of available AI types with full bot info.
    /// </summary>
    public IReadOnlyList<AiTypeInfo> GetAvailableTypes() =>
        _bots.Values.Select(b => new AiTypeInfo(
            b.AgentType, b.DisplayName, b.Difficulty, b.Rating,
            b.Pun, b.Description, b.Author)).ToList();

    /// <summary>
    /// Creates an AI player agent of the specified type for the given position.
    /// Falls back to the strongest active bot if the type is not found.
    /// </summary>
    public IPlayerAgent CreateAgent(string aiType, PlayerPosition position)
    {
        if (_bots.TryGetValue(aiType, out var bot))
            return bot.Factory.Create(position);

        if (_defaultAgentType != null && _bots.TryGetValue(_defaultAgentType, out var defaultBot))
        {
            _logger.LogWarning("Unknown AI type {AiType}, falling back to {Default}", aiType, _defaultAgentType);
            return defaultBot.Factory.Create(position);
        }

        throw new InvalidOperationException(
            $"No bot found for type '{aiType}' and no default bot available. Run sync-bots first.");
    }

    /// <summary>
    /// Gets the agent type of the strongest active bot (highest difficulty).
    /// Used as default for unassigned seats.
    /// </summary>
    public string GetDefaultAgentType() =>
        _defaultAgentType
        ?? throw new InvalidOperationException("No active bots available. Run sync-bots first.");

    public string GetDisplayName(string aiType) =>
        _bots.TryGetValue(aiType, out var bot) ? bot.DisplayName : aiType;

    public void Dispose()
    {
        foreach (var bot in _bots.Values)
        {
            if (bot.Factory is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private sealed record CachedBot(
        string AgentType,
        string DisplayName,
        short Difficulty,
        int Rating,
        string? Pun,
        string? Description,
        string? Author,
        IPlayerAgentFactory Factory);
}

public record AiTypeInfo(
    string Name,
    string DisplayName,
    short Difficulty,
    int Rating,
    string? Pun,
    string? Description,
    string? Author);
