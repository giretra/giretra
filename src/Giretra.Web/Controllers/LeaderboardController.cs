using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet]
    public async Task<ActionResult<LeaderboardResponse>> GetLeaderboard()
    {
        var result = await _leaderboardService.GetLeaderboardAsync();
        return Ok(result);
    }

    [HttpGet("players/{playerId:guid}")]
    public async Task<ActionResult<PlayerProfileResponse>> GetPlayerProfile(Guid playerId)
    {
        var profile = await _leaderboardService.GetPlayerProfileAsync(playerId);
        if (profile == null)
            return NotFound();
        return Ok(profile);
    }
}
