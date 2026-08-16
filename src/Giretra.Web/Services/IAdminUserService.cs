using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public interface IAdminUserService
{
    Task<AdminUserListResponse> GetUsersAsync(string? search, int page, int pageSize);
    Task<(bool Success, string? Error)> BanAsync(Guid userId, string? reason);
    Task<(bool Success, string? Error)> UnbanAsync(Guid userId);
    Task<(bool Success, string? Error)> ClearDisplayNameAsync(Guid userId);
}
