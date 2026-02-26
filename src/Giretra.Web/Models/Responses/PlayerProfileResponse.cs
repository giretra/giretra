namespace Giretra.Web.Models.Responses;

public sealed class PlayerProfileResponse
{
    // Common
    public required string DisplayName { get; init; }
    public required bool IsBot { get; init; }
    public required int GamesPlayed { get; init; }
    public required int GamesWon { get; init; }
    public required int WinStreak { get; init; }
    public required int BestWinStreak { get; init; }

    // Human-only (null for bots)
    public string? AvatarUrl { get; init; }
    public int? EloRating { get; init; }
    public DateTimeOffset? MemberSince { get; init; }

    // Bot-only (null for humans)
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? AuthorGithubUrl { get; init; }
    public string? Pun { get; init; }
    public short? Difficulty { get; init; }
    public int? BotRating { get; init; }
    public string? BotType { get; init; }
}
