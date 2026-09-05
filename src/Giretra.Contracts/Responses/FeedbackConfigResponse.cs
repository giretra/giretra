namespace Giretra.Web.Models.Responses;

/// <summary>
/// Tells the client which feedback channels are available.
/// </summary>
public sealed class FeedbackConfigResponse
{
    /// <summary>
    /// True when the server can deliver contact-form messages (mail transport configured and at least one recipient).
    /// </summary>
    public required bool ContactEnabled { get; init; }

    /// <summary>
    /// Where to open a GitHub issue, for players who prefer that route.
    /// </summary>
    public required string GitHubIssuesUrl { get; init; }
}
