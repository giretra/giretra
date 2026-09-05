using Giretra.Model.Entities;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

/// <summary>
/// In-app contact form: lets players reach the moderators without a GitHub account.
/// </summary>
[ApiController]
[Route("api/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpGet("config")]
    public async Task<ActionResult<FeedbackConfigResponse>> GetConfig(CancellationToken cancellationToken)
    {
        return Ok(await _feedbackService.GetConfigAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Send([FromBody] SendFeedbackRequest request, CancellationToken cancellationToken)
    {
        var user = (User)HttpContext.Items["GiretraUser"]!;
        var context = new FeedbackContext(Request.Headers.UserAgent.ToString());

        var result = await _feedbackService.SendAsync(user, request, context, cancellationToken);

        return result.Outcome switch
        {
            FeedbackOutcome.Sent => NoContent(),
            FeedbackOutcome.Invalid => BadRequest(new { error = result.Error }),
            FeedbackOutcome.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { error = result.Error }),
            FeedbackOutcome.NotConfigured => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = result.Error }),
            _ => StatusCode(StatusCodes.Status502BadGateway, new { error = result.Error })
        };
    }
}
