using Giretra.Model.Entities;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    /// <summary>
    /// Gets the replay of one deal of a finished match. Only participants of the match can
    /// request it; abandoned matches are not replayable.
    /// </summary>
    [HttpGet("{matchId:guid}/deals/{dealNumber:int}/replay")]
    [Authorize]
    [ProducesResponseType(typeof(DealReplayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DealReplayResponse>> GetDealReplay(
        Guid matchId,
        int dealNumber,
        [FromServices] IDealReplayService replayService)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        var result = await replayService.GetReplayAsync(matchId, dealNumber, user.Id);

        return result.Status switch
        {
            DealReplayStatus.Ok => Ok(result.Replay),
            DealReplayStatus.Forbidden => Forbid(),
            DealReplayStatus.NotReplayable => UnprocessableEntity(new ProblemDetails
            {
                Title = "Deal is not replayable",
                Detail = "The match was abandoned or the deal has no complete play record.",
            }),
            _ => NotFound(),
        };
    }
}
