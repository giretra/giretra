using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IBlockService
{
    Task<IReadOnlyList<BlockedUserResponse>> GetBlockedUsersAsync(Guid userId);
    Task<(bool Success, string? Error)> BlockUserAsync(Guid userId, string username, string? reason);
    Task<(bool Success, string? Error)> UnblockUserAsync(Guid userId, Guid blockId);
}
