namespace Giretra.Web.Models.Responses;

public sealed class FriendRequestResponse
{
    public required Guid FriendshipId { get; init; }
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required DateTimeOffset SentAt { get; init; }
}
