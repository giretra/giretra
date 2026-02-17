using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class BlockService : IBlockService
{
    private readonly GiretraDbContext _db;

    public BlockService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BlockedUserResponse>> GetBlockedUsersAsync(Guid userId)
    {
        return await _db.Blocks
            .Include(b => b.Blocked)
            .Where(b => b.BlockerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BlockedUserResponse
            {
                BlockId = b.Id,
                UserId = b.Blocked.Id,
                Username = b.Blocked.Username,
                DisplayName = b.Blocked.DisplayName,
                BlockedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> BlockUserAsync(Guid userId, string username, string? reason)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (targetUser == null)
            return (false, "User not found.");

        if (targetUser.Id == userId)
            return (false, "You cannot block yourself.");

        var alreadyBlocked = await _db.Blocks.AnyAsync(b =>
            b.BlockerId == userId && b.BlockedId == targetUser.Id);
        if (alreadyBlocked)
            return (false, "User is already blocked.");

        // Auto-delete any friendship between both users
        var friendships = await _db.Friendships
            .Where(f => (f.RequesterId == userId && f.AddresseeId == targetUser.Id) ||
                        (f.RequesterId == targetUser.Id && f.AddresseeId == userId))
            .ToListAsync();
        _db.Friendships.RemoveRange(friendships);

        // Create block
        var block = new Block
        {
            BlockerId = userId,
            BlockedId = targetUser.Id,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Blocks.Add(block);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UnblockUserAsync(Guid userId, Guid blockId)
    {
        var block = await _db.Blocks.FindAsync(blockId);
        if (block == null)
            return (false, "Block not found.");

        if (block.BlockerId != userId)
            return (false, "You can only unblock users you have blocked.");

        _db.Blocks.Remove(block);
        await _db.SaveChangesAsync();

        return (true, null);
    }
}
