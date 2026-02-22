namespace Giretra.Web.Models.Responses;

public sealed class LeaderboardEntryResponse
{
    public required int Rank { get; set; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required int Rating { get; init; }
    public required int GamesPlayed { get; init; }
    public required double WinRate { get; init; }
    public required bool IsBot { get; init; }
}
