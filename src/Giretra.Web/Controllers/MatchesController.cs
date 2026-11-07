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

    /// <summary>
    /// Gets the scoreboard of a finished match: every deal with its contract, negotiation,
    /// points and trick winners, plus match-level statistics. Participants only.
    /// </summary>
    [HttpGet("{matchId:guid}/summary")]
    [Authorize]
    [ProducesResponseType(typeof(MatchSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MatchSummaryResponse>> GetSummary(
        Guid matchId,
        [FromServices] IMatchSummaryService summaryService)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        var result = await summaryService.GetSummaryAsync(matchId, user.Id);

        return result.Status switch
        {
            MatchSummaryStatus.Ok => Ok(result.Summary),
            MatchSummaryStatus.Forbidden => Forbid(),
            MatchSummaryStatus.NotCompleted => UnprocessableEntity(new ProblemDetails
            {
                Title = "Match is not finished",
                Detail = "The scoreboard is only available once the match is over.",
            }),
            _ => NotFound(),
        };
    }
}
