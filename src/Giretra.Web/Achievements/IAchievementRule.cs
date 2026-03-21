namespace Giretra.Web.Achievements;

/// <summary>
/// Defines an achievement that can be earned by a player.
/// Each implementation is auto-discovered and synced to the database on startup.
/// Adding a new achievement = adding a new class implementing this interface.
/// </summary>
public interface IAchievementRule
{
    /// <summary>
    /// Stable unique identifier. Hardcode as a constant in each implementation.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Unique code used as the i18n key on the Angular side.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Grouping category (e.g. "scoring", "streaks", "modes").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Prestige level. Higher = more prestigious.
    /// </summary>
    int Tier { get; }

    /// <summary>
    /// Optional icon identifier for the frontend.
    /// </summary>
    string? IconName { get; }

    /// <summary>
    /// When true, the description is hidden on the frontend until the player earns it.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Display ordering within a category.
    /// </summary>
    int SortOrder { get; }

    /// <summary>
    /// When this achievement should be evaluated.
    /// </summary>
    AchievementTrigger Trigger { get; }

    /// <summary>
    /// Returns true if the achievement is earned given the current context.
    /// </summary>
    Task<bool> IsEarnedAsync(AchievementContext context);
}
