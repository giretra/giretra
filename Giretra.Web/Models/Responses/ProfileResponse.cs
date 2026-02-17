namespace Giretra.Web.Models.Responses;

public sealed class ProfileResponse
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required int EloRating { get; init; }
    public required bool EloIsPublic { get; init; }
    public required int GamesPlayed { get; init; }
    public required int GamesWon { get; init; }
    public required int WinStreak { get; init; }
    public required int BestWinStreak { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
