namespace Giretra.Web.Models.Responses;

public sealed class MatchHistoryListResponse
{
    public required IReadOnlyList<MatchHistoryItemResponse> Matches { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
