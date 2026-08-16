using Giretra.Model.Entities;
using Giretra.Web.Auth;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthPolicies.Moderator)]
public class AdminController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly IProfileService _profileService;

    public AdminController(IAdminUserService adminUserService, IProfileService profileService)
    {
        _adminUserService = adminUserService;
        _profileService = profileService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<AdminUserListResponse>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var users = await _adminUserService.GetUsersAsync(search, page, pageSize);
        return Ok(users);
    }

    [HttpPost("users/{userId}/ban")]
    public async Task<ActionResult> BanUser(Guid userId, [FromBody] BanUserRequest request)
    {
        if (userId == GetAuthenticatedUser().Id)
            return BadRequest(new { error = "You cannot ban yourself." });

        var (success, error) = await _adminUserService.BanAsync(userId, request.Reason);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    [HttpPost("users/{userId}/unban")]
    public async Task<ActionResult> UnbanUser(Guid userId)
    {
        var (success, error) = await _adminUserService.UnbanAsync(userId);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    [HttpPost("users/{userId}/clear-display-name")]
    public async Task<ActionResult> ClearDisplayName(Guid userId)
    {
        var (success, error) = await _adminUserService.ClearDisplayNameAsync(userId);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    [HttpDelete("users/{userId}/avatar")]
    public async Task<ActionResult> RemoveAvatar(Guid userId)
    {
        await _profileService.DeleteAvatarAsync(userId);
        return NoContent();
    }

    private User GetAuthenticatedUser() => (User)HttpContext.Items["GiretraUser"]!;
}
