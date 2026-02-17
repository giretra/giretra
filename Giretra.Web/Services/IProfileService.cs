using Giretra.Web.Models.Responses;
using Microsoft.AspNetCore.Http;

namespace Giretra.Web.Services;

public interface IProfileService
{
    Task<ProfileResponse> GetProfileAsync(Guid userId);
    Task<(bool Success, string? Error)> UpdateDisplayNameAsync(Guid userId, string displayName);
    Task<(bool Success, string? AvatarUrl, string? Error)> UpdateAvatarAsync(Guid userId, IFormFile file);
    Task DeleteAvatarAsync(Guid userId);
    Task UpdateEloVisibilityAsync(Guid userId, bool isPublic);
}
