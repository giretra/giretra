namespace Giretra.Web.Models.Responses;

public sealed class FriendsListResponse
{
    public required IReadOnlyList<FriendResponse> Friends { get; init; }
    public required IReadOnlyList<FriendRequestResponse> PendingReceived { get; init; }
    public required IReadOnlyList<FriendRequestResponse> PendingSent { get; init; }
}
