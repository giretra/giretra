namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a player's achievement showcase.
/// </summary>
public sealed class AchievementShowcaseResponse
{
    public required string PlayerName { get; init; }
    public int EarnedCount { get; init; }
    public int TotalCount { get; init; }
    public required List<AchievementShowcaseItem> Achievements { get; init; }
    public List<string> QualifyingBots { get; init; } = [];
}

/// <summary>
/// One achievement in a showcase, earned or not.
/// </summary>
public sealed class AchievementShowcaseItem
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
    public required bool IsEarned { get; init; }
    public DateTimeOffset? EarnedAt { get; init; }
}

/// <summary>
/// Response DTO for an achievement definition.
/// </summary>
public sealed class AchievementResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
}

/// <summary>
/// Response DTO for an achievement earned by a player.
/// </summary>
public sealed class PlayerAchievementResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
    public required Guid MatchId { get; init; }
    public short? DealNumber { get; init; }
}
