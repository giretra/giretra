using Giretra.Model;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Achievements;

/// <summary>
/// On startup, syncs IAchievementRule definitions from code into the Achievement DB table.
/// New rules are inserted, changed metadata is updated, removed rules are deactivated.
/// </summary>
public sealed class AchievementSyncService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnumerable<IAchievementRule> _rules;
    private readonly ILogger<AchievementSyncService> _logger;

    public AchievementSyncService(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IAchievementRule> rules,
        ILogger<AchievementSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _rules = rules;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiretraDbContext>();

        var existingByCode = await db.Achievements.ToDictionaryAsync(a => a.Code, cancellationToken);
        var rulesByCode = _rules.ToDictionary(r => r.Code);
        var now = DateTimeOffset.UtcNow;
        var changes = 0;

        // Insert new or update existing
        foreach (var rule in _rules)
        {
            if (existingByCode.TryGetValue(rule.Code, out var existing))
            {
                var changed = false;

                if (existing.Category != rule.Category) { existing.Category = rule.Category; changed = true; }
                if (existing.Tier != rule.Tier) { existing.Tier = rule.Tier; changed = true; }
                if (existing.IconName != rule.IconName) { existing.IconName = rule.IconName; changed = true; }
                if (existing.IsHidden != rule.IsHidden) { existing.IsHidden = rule.IsHidden; changed = true; }
                if (existing.SortOrder != rule.SortOrder) { existing.SortOrder = rule.SortOrder; changed = true; }
                if (!existing.IsActive) { existing.IsActive = true; changed = true; }

                // Ensure the DB Id matches the rule's hardcoded Id
                if (existing.Id != rule.Id)
                {
                    _logger.LogWarning(
                        "Achievement {Code} has DB Id {DbId} but rule defines {RuleId}. Keeping DB Id.",
                        rule.Code, existing.Id, rule.Id);
                }

                if (changed)
                {
                    existing.UpdatedAt = now;
                    changes++;
                }
            }
            else
            {
                db.Achievements.Add(new Model.Entities.Achievement
                {
                    Id = rule.Id,
                    Code = rule.Code,
                    Category = rule.Category,
                    Tier = rule.Tier,
                    IconName = rule.IconName,
                    IsHidden = rule.IsHidden,
                    SortOrder = rule.SortOrder,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                changes++;
            }
        }

        // Deactivate rules that no longer exist in code
        foreach (var (code, entity) in existingByCode)
        {
            if (entity.IsActive && !rulesByCode.ContainsKey(code))
            {
                entity.IsActive = false;
                entity.UpdatedAt = now;
                changes++;
            }
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Achievement sync: {Changes} change(s) applied, {Total} rule(s) active",
                changes, rulesByCode.Count);
        }
        else
        {
            _logger.LogInformation("Achievement sync: {Total} rule(s) up to date", rulesByCode.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
