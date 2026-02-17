namespace Giretra.Web.Models.Responses;

public sealed class UserSearchResultResponse
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}
