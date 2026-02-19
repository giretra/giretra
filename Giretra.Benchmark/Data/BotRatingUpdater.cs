using Giretra.Core.Players;
using Giretra.Model;
using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Benchmark.Data;

/// <summary>
/// Persists bot entries and adjusted ELO ratings to the Bot table in the database.
/// </summary>
public static class BotRatingUpdater
{
    public static async Task SaveAdjustedRatingsAsync(
        string connectionString,
        IReadOnlyList<(IPlayerAgentFactory Factory, int AdjustedRating)> ratings)
    {
        await using var context = CreateContext(connectionString);

        var agentTypes = ratings.Select(r => r.Factory.AgentName).ToList();
        var existingBots = await context.Bots
            .Where(b => agentTypes.Contains(b.AgentType))
            .ToDictionaryAsync(b => b.AgentType);

        foreach (var (factory, adjustedRating) in ratings)
        {
            if (existingBots.TryGetValue(factory.AgentName, out var bot))
            {
                UpdateBot(bot, factory);
                bot.Rating = adjustedRating;
            }
            else
            {
                context.Bots.Add(CreateBot(factory, adjustedRating));
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SyncBotsAsync(
        string connectionString,
        IReadOnlyList<IPlayerAgentFactory> factories)
    {
        await using var context = CreateContext(connectionString);

        var agentTypes = factories.Select(f => f.AgentName).ToList();
        var existingBots = await context.Bots
            .Where(b => agentTypes.Contains(b.AgentType))
            .ToDictionaryAsync(b => b.AgentType);

        foreach (var factory in factories)
        {
            if (existingBots.TryGetValue(factory.AgentName, out var bot))
                UpdateBot(bot, factory);
            else
                context.Bots.Add(CreateBot(factory, rating: 1000));
        }

        await context.SaveChangesAsync();
    }

    private static GiretraDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new GiretraDbContext(options);
    }

    private static void UpdateBot(Bot bot, IPlayerAgentFactory factory)
    {
        bot.DisplayName = factory.DisplayName;
        bot.AgentTypeFactory = factory.GetType().FullName!;
        bot.Pun = string.IsNullOrEmpty(factory.Pun) ? null : factory.Pun;
    }

    private static Bot CreateBot(IPlayerAgentFactory factory, int rating) => new()
    {
        Id = factory.Identifier,
        AgentType = factory.AgentName,
        AgentTypeFactory = factory.GetType().FullName!,
        DisplayName = factory.DisplayName,
        Pun = string.IsNullOrEmpty(factory.Pun) ? null : factory.Pun,
        Rating = rating,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
