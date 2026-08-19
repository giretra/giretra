namespace Giretra.Web.Models.Responses;

public sealed class UserSearchResponse
{
    public required IReadOnlyList<UserSearchResultResponse> Results { get; init; }
}
