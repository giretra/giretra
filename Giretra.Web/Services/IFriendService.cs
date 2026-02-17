using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IFriendService
{
    Task<FriendsListResponse> GetFriendsAsync(Guid userId);
    Task<(bool Success, string? Error)> SendFriendRequestAsync(Guid userId, string username);
    Task<(bool Success, string? Error)> AcceptFriendRequestAsync(Guid userId, Guid friendshipId);
    Task<(bool Success, string? Error)> DeclineFriendRequestAsync(Guid userId, Guid friendshipId);
    Task<(bool Success, string? Error)> RemoveFriendAsync(Guid userId, Guid friendUserId);
    Task<UserSearchResponse> SearchUsersAsync(Guid userId, string query);
    Task<int> GetPendingCountAsync(Guid userId);
}
