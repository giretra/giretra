using Giretra.Core.Players;

namespace Giretra.Web.Models.Events;

/// <summary>
/// Event sent after match persistence when achievements have been earned.
/// Fired separately from MatchEnded because achievements are evaluated during persistence,
/// which happens after the MatchEnded event.
/// </summary>
public sealed class AchievementsEarnedEvent
{
    public required string GameId { get; init; }
    public required IReadOnlyList<AchievementEarnedDto> Achievements { get; init; }
}

public sealed class AchievementEarnedDto
{
    public required PlayerPosition PlayerPosition { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public bool IsHidden { get; init; }
    public short? DealNumber { get; init; }
}
