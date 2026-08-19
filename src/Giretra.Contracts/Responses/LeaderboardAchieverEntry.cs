namespace Giretra.Web.Models.Responses;

public sealed class LeaderboardAchieverEntry
{
    public required Guid PlayerId { get; init; }
    public required int Rank { get; set; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required int AchievementPoints { get; init; }
    public required int AchievementCount { get; init; }
}
