namespace Giretra.Web.Models.Responses;

public sealed class LeaderboardBotEntry
{
    public required Guid PlayerId { get; init; }
    public required int Rank { get; set; }
    public required string DisplayName { get; init; }
    public required int Rating { get; init; }
    public required int GamesPlayed { get; init; }
    public required double WinRate { get; init; }
    public string? Author { get; init; }
    public required short Difficulty { get; init; }
}
