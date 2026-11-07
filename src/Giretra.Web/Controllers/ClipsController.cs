using Giretra.Model.Entities;
using Giretra.Web.Auth;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/clips")]
public class ClipsController : ControllerBase
{
    private readonly IClipService _clips;
    private readonly IAuthorizationService _authorization;

    public ClipsController(IClipService clips, IAuthorizationService authorization)
    {
        _clips = clips;
        _authorization = authorization;
    }

    /// <summary>
    /// Queues a clip of a trick range for rendering. Participants of completed matches only.
    /// Returns the existing clip when an identical one already exists.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ClipResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ClipResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClipResponse>> Create([FromBody] CreateClipRequest request)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        var result = await _clips.CreateAsync(user.Id, request, Origin());

        return result.Status switch
        {
            ClipCreateStatus.Created => CreatedAtAction(nameof(Get), new { shortId = result.Clip!.ShortId }, result.Clip),
            ClipCreateStatus.Existing => Ok(result.Clip),
            ClipCreateStatus.InvalidRequest => BadRequest(new ProblemDetails { Title = "Invalid clip", Detail = result.Error }),
            ClipCreateStatus.Forbidden => Forbid(),
            ClipCreateStatus.MatchNotFound => NotFound(),
            ClipCreateStatus.NotReplayable => UnprocessableEntity(new ProblemDetails
            {
                Title = "Deal is not replayable",
                Detail = "The match was abandoned or the deal has no complete play record.",
            }),
            ClipCreateStatus.Throttled => StatusCode(StatusCodes.Status429TooManyRequests,
                new ProblemDetails { Title = "Too many clips", Detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>Clips created by the current user.</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<ClipListResponse>> Mine()
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        return Ok(await _clips.ListMineAsync(user.Id, Origin()));
    }

    /// <summary>Public view of a clip, with the replay payload for the interactive page.</summary>
    [HttpGet("{shortId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<ClipResponse>> Get(string shortId)
    {
        var clip = await _clips.GetPublicAsync(shortId, Origin());
        if (clip == null)
            return NotFound();
        if (clip.Status == ClipStatusResponse.Removed)
            return StatusCode(StatusCodes.Status410Gone);
        return Ok(clip);
    }

    /// <summary>Takes a clip down. Creator or moderator.</summary>
    [HttpDelete("{shortId}")]
    [Authorize]
    public async Task<ActionResult> Delete(string shortId)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        var isModerator = (await _authorization.AuthorizeAsync(User, AuthPolicies.Moderator)).Succeeded;
        var result = await _clips.DeleteAsync(user.Id, shortId, isModerator);
        return result switch
        {
            ClipDeleteStatus.Ok => NoContent(),
            ClipDeleteStatus.Forbidden => Forbid(),
            _ => NotFound(),
        };
    }

    private PublicOrigin Origin() => ClipPublicOrigin.Resolve(HttpContext);
}
