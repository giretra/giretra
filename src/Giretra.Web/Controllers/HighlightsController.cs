using Giretra.Model.Entities;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HighlightsController : ControllerBase
{
    /// <summary>
    /// Gets the personal statistics dashboard for the current authenticated user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<HighlightsResponse>> GetMine(
        [FromServices] IHighlightsService highlightsService)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        return Ok(await highlightsService.GetHighlightsAsync(user.Id));
    }

    /// <summary>
    /// Gets the public statistics dashboard for any player (Elo hidden unless public).
    /// </summary>
    [HttpGet("players/{playerId:guid}")]
    public async Task<ActionResult<HighlightsResponse>> GetPlayer(
        Guid playerId,
        [FromServices] IHighlightsService highlightsService)
    {
        var response = await highlightsService.GetPlayerHighlightsAsync(playerId);
        if (response == null)
            return NotFound();

        return Ok(response);
    }
}
