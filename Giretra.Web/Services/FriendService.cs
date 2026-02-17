using Giretra.Model;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class FriendService : IFriendService
{
    private readonly GiretraDbContext _db;

    public FriendService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<FriendsListResponse> GetFriendsAsync(Guid userId)
    {
        var friendships = await _db.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
            .ToListAsync();

        var friends = friendships
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Select(f =>
            {
                var other = f.RequesterId == userId ? f.Addressee : f.Requester;
                return new FriendResponse
                {
                    UserId = other.Id,
                    Username = other.Username,
                    DisplayName = other.DisplayName,
                    AvatarUrl = other.AvatarUrl,
                    FriendsSince = f.UpdatedAt
                };
            })
            .OrderBy(f => f.DisplayName)
            .ToList();

        var pendingReceived = friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == userId)
            .Select(f => new FriendRequestResponse
            {
                FriendshipId = f.Id,
                UserId = f.Requester.Id,
                Username = f.Requester.Username,
                DisplayName = f.Requester.DisplayName,
                AvatarUrl = f.Requester.AvatarUrl,
                SentAt = f.CreatedAt
            })
            .OrderByDescending(f => f.SentAt)
            .ToList();

        var pendingSent = friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == userId)
            .Select(f => new FriendRequestResponse
            {
                FriendshipId = f.Id,
                UserId = f.Addressee.Id,
                Username = f.Addressee.Username,
                DisplayName = f.Addressee.DisplayName,
                AvatarUrl = f.Addressee.AvatarUrl,
                SentAt = f.CreatedAt
            })
            .OrderByDescending(f => f.SentAt)
            .ToList();

        return new FriendsListResponse
        {
            Friends = friends,
            PendingReceived = pendingReceived,
            PendingSent = pendingSent
        };
    }

    public async Task<(bool Success, string? Error)> SendFriendRequestAsync(Guid userId, string username)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (targetUser == null)
            return (false, "User not found.");

        if (targetUser.Id == userId)
            return (false, "You cannot send a friend request to yourself.");

        // Check if blocked in either direction
        var isBlocked = await _db.Blocks.AnyAsync(b =>
            (b.BlockerId == userId && b.BlockedId == targetUser.Id) ||
            (b.BlockerId == targetUser.Id && b.BlockedId == userId));
        if (isBlocked)
            return (false, "Unable to send friend request.");

        // Check existing friendship
        var existing = await _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.AddresseeId == targetUser.Id) ||
            (f.RequesterId == targetUser.Id && f.AddresseeId == userId));

        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                return (false, "You are already friends.");

            if (existing.Status == FriendshipStatus.Pending)
            {
                // If the other user already sent us a request, auto-accept
                if (existing.RequesterId == targetUser.Id && existing.AddresseeId == userId)
                {
                    existing.Status = FriendshipStatus.Accepted;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync();
                    return (true, null);
                }

                return (false, "Friend request already sent.");
            }

            if (existing.Status == FriendshipStatus.Declined)
            {
                // Reset declined request to pending
                existing.Status = FriendshipStatus.Pending;
                existing.RequesterId = userId;
                existing.AddresseeId = targetUser.Id;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                return (true, null);
            }
        }

        // Create new friendship request
        var friendship = new Model.Entities.Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUser.Id,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Friendships.Add(friendship);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AcceptFriendRequestAsync(Guid userId, Guid friendshipId)
    {
        var friendship = await _db.Friendships.FindAsync(friendshipId);
        if (friendship == null)
            return (false, "Friend request not found.");

        if (friendship.AddresseeId != userId)
            return (false, "You can only accept requests sent to you.");

        if (friendship.Status != FriendshipStatus.Pending)
            return (false, "This request is no longer pending.");

        friendship.Status = FriendshipStatus.Accepted;
        friendship.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeclineFriendRequestAsync(Guid userId, Guid friendshipId)
    {
        var friendship = await _db.Friendships.FindAsync(friendshipId);
        if (friendship == null)
            return (false, "Friend request not found.");

        if (friendship.AddresseeId != userId && friendship.RequesterId != userId)
            return (false, "You are not part of this friend request.");

        if (friendship.Status != FriendshipStatus.Pending)
            return (false, "This request is no longer pending.");

        friendship.Status = FriendshipStatus.Declined;
        friendship.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveFriendAsync(Guid userId, Guid friendUserId)
    {
        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == userId && f.AddresseeId == friendUserId) ||
             (f.RequesterId == friendUserId && f.AddresseeId == userId)));

        if (friendship == null)
            return (false, "Friendship not found.");

        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<UserSearchResponse> SearchUsersAsync(Guid userId, string query)
    {
        var normalizedQuery = query.Trim().ToLower();

        // Get blocked user IDs in both directions
        var blockedIds = await _db.Blocks
            .Where(b => b.BlockerId == userId || b.BlockedId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedId : b.BlockerId)
            .ToListAsync();

        var excludeIds = new HashSet<Guid>(blockedIds) { userId };

        var results = await _db.Users
            .Where(u => !excludeIds.Contains(u.Id) &&
                        EF.Functions.ILike(u.Username, $"%{normalizedQuery}%"))
            .OrderBy(u => u.Username)
            .Take(20)
            .Select(u => new UserSearchResultResponse
            {
                UserId = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            })
            .ToListAsync();

        return new UserSearchResponse { Results = results };
    }

    public async Task<int> GetPendingCountAsync(Guid userId)
    {
        return await _db.Friendships.CountAsync(f =>
            f.AddresseeId == userId && f.Status == FriendshipStatus.Pending);
    }
}
