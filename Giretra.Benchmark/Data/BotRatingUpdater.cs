using Giretra.Core.Players;
using Giretra.Model;
using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Benchmark.Data;

/// <summary>
/// Persists adjusted ELO ratings to the Bot table in the database.
/// </summary>
public static class BotRatingUpdater
{
    public static async Task SaveAdjustedRatingsAsync(
        string connectionString,
        IReadOnlyList<(IPlayerAgentFactory Factory, int AdjustedRating)> ratings)
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new GiretraDbContext(options);

        var agentTypes = ratings.Select(r => r.Factory.AgentName).ToList();
        var existingBots = await context.Bots
            .Where(b => agentTypes.Contains(b.AgentType))
            .ToDictionaryAsync(b => b.AgentType);

        foreach (var (factory, adjustedRating) in ratings)
        {
            if (existingBots.TryGetValue(factory.AgentName, out var bot))
            {
                bot.Rating = adjustedRating;
                bot.DisplayName = factory.DisplayName;
                bot.AgentTypeFactory = factory.GetType().FullName!;
                bot.Pun = string.IsNullOrEmpty(factory.Pun) ? null : factory.Pun;
            }
            else
            {
                context.Bots.Add(new Bot
                {
                    Id = factory.Identifier,
                    AgentType = factory.AgentName,
                    AgentTypeFactory = factory.GetType().FullName!,
                    DisplayName = factory.DisplayName,
                    Pun = string.IsNullOrEmpty(factory.Pun) ? null : factory.Pun,
                    Rating = adjustedRating,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
