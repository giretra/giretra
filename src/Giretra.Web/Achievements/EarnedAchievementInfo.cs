using Giretra.Core.Players;

namespace Giretra.Web.Achievements;

/// <summary>
/// Describes an achievement earned during a game session.
/// Accumulated in GameSession and sent in MatchEndedEvent.
/// </summary>
public sealed record EarnedAchievementInfo(
    PlayerPosition PlayerPosition,
    string Code,
    string Name,
    string Category,
    int Tier,
    string? IconName,
    bool IsHidden,
    short? DealNumber);
