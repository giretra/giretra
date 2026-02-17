namespace Giretra.Web.Models.Responses;

public sealed class LeaderboardResponse
{
    public required IReadOnlyList<LeaderboardEntryResponse> Entries { get; init; }
    public required int TotalCount { get; init; }
}
