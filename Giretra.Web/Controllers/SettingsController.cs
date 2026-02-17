using Giretra.Model.Entities;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IFriendService _friendService;
    private readonly IBlockService _blockService;
    private readonly IMatchHistoryService _matchHistoryService;

    public SettingsController(
        IProfileService profileService,
        IFriendService friendService,
        IBlockService blockService,
        IMatchHistoryService matchHistoryService)
    {
        _profileService = profileService;
        _friendService = friendService;
        _blockService = blockService;
        _matchHistoryService = matchHistoryService;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Current User
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("/api/me")]
    public ActionResult GetMe()
    {
        var user = GetAuthenticatedUser();
        return Ok(new { displayName = user.EffectiveDisplayName });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Profile
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var user = GetAuthenticatedUser();
        var profile = await _profileService.GetProfileAsync(user.Id);
        return Ok(profile);
    }

    [HttpPut("profile/display-name")]
    public async Task<ActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _profileService.UpdateDisplayNameAsync(user.Id, request.DisplayName);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    [HttpPost("profile/avatar")]
    public async Task<ActionResult> UploadAvatar(IFormFile file)
    {
        var user = GetAuthenticatedUser();
        var (success, avatarUrl, error) = await _profileService.UpdateAvatarAsync(user.Id, file);
        if (!success)
            return BadRequest(new { error });

        return Ok(new { avatarUrl });
    }

    [HttpDelete("profile/avatar")]
    public async Task<ActionResult> DeleteAvatar()
    {
        var user = GetAuthenticatedUser();
        await _profileService.DeleteAvatarAsync(user.Id);
        return NoContent();
    }

    [HttpPut("profile/elo-visibility")]
    public async Task<ActionResult> UpdateEloVisibility([FromBody] UpdateEloVisibilityRequest request)
    {
        var user = GetAuthenticatedUser();
        await _profileService.UpdateEloVisibilityAsync(user.Id, request.IsPublic);
        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Friends
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("friends")]
    public async Task<ActionResult<FriendsListResponse>> GetFriends()
    {
        var user = GetAuthenticatedUser();
        var friends = await _friendService.GetFriendsAsync(user.Id);
        return Ok(friends);
    }

    [HttpGet("friends/pending-count")]
    public async Task<ActionResult<PendingCountResponse>> GetPendingCount()
    {
        var user = GetAuthenticatedUser();
        var count = await _friendService.GetPendingCountAsync(user.Id);
        return Ok(new PendingCountResponse { Count = count });
    }

    [HttpPost("friends/request")]
    public async Task<ActionResult> SendFriendRequest([FromBody] SendFriendRequestRequest request)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _friendService.SendFriendRequestAsync(user.Id, request.Username);
        if (!success)
            return BadRequest(new { error });

        return Ok();
    }

    [HttpPost("friends/{friendshipId}/accept")]
    public async Task<ActionResult> AcceptFriendRequest(Guid friendshipId)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _friendService.AcceptFriendRequestAsync(user.Id, friendshipId);
        if (!success)
            return BadRequest(new { error });

        return Ok();
    }

    [HttpPost("friends/{friendshipId}/decline")]
    public async Task<ActionResult> DeclineFriendRequest(Guid friendshipId)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _friendService.DeclineFriendRequestAsync(user.Id, friendshipId);
        if (!success)
            return BadRequest(new { error });

        return Ok();
    }

    [HttpDelete("friends/{friendUserId}")]
    public async Task<ActionResult> RemoveFriend(Guid friendUserId)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _friendService.RemoveFriendAsync(user.Id, friendUserId);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    [HttpGet("friends/search")]
    public async Task<ActionResult<UserSearchResponse>> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new UserSearchResponse { Results = [] });

        var user = GetAuthenticatedUser();
        var results = await _friendService.SearchUsersAsync(user.Id, q);
        return Ok(results);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Blocked
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("blocked")]
    public async Task<ActionResult<IReadOnlyList<BlockedUserResponse>>> GetBlockedUsers()
    {
        var user = GetAuthenticatedUser();
        var blocked = await _blockService.GetBlockedUsersAsync(user.Id);
        return Ok(blocked);
    }

    [HttpPost("blocked")]
    public async Task<ActionResult> BlockUser([FromBody] BlockUserRequest request)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _blockService.BlockUserAsync(user.Id, request.Username, request.Reason);
        if (!success)
            return BadRequest(new { error });

        return Ok();
    }

    [HttpDelete("blocked/{blockId}")]
    public async Task<ActionResult> UnblockUser(Guid blockId)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = await _blockService.UnblockUserAsync(user.Id, blockId);
        if (!success)
            return BadRequest(new { error });

        return NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Match History
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("matches")]
    public async Task<ActionResult<MatchHistoryListResponse>> GetMatchHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var user = GetAuthenticatedUser();
        var history = await _matchHistoryService.GetMatchHistoryAsync(user.Id, page, pageSize);
        return Ok(history);
    }

    private User GetAuthenticatedUser() => (User)HttpContext.Items["GiretraUser"]!;
}
