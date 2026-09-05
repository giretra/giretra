using Giretra.Model.Entities;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services;

public enum FeedbackOutcome
{
    Sent,
    Invalid,
    RateLimited,
    /// <summary>No mail transport or nobody to deliver to.</summary>
    NotConfigured,
    /// <summary>The transport failed; the player should retry later or use GitHub.</summary>
    Failed
}

public sealed record FeedbackResult(FeedbackOutcome Outcome, string? Error = null)
{
    public static readonly FeedbackResult Success = new(FeedbackOutcome.Sent);
}

/// <summary>
/// Extra request context forwarded to moderators with the message.
/// </summary>
public sealed record FeedbackContext(string? UserAgent);

public interface IFeedbackService
{
    Task<FeedbackConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<FeedbackResult> SendAsync(User sender, SendFeedbackRequest request, FeedbackContext context, CancellationToken cancellationToken = default);
}
