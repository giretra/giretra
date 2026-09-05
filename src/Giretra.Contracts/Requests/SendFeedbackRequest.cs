namespace Giretra.Web.Models.Requests;

/// <summary>
/// What kind of message the player is sending through the in-app contact form.
/// </summary>
public enum FeedbackCategory
{
    Bug,
    Idea,
    Question,
    Other
}

/// <summary>
/// Message submitted from the in-app contact form. It is e-mailed to the moderators
/// (and any extra recipients configured on the server) with the sender's account attached.
/// </summary>
public sealed class SendFeedbackRequest
{
    public required FeedbackCategory Category { get; init; }

    public required string Subject { get; init; }

    public required string Message { get; init; }

    /// <summary>
    /// In-app URL the player was on when they opened the form (optional context for moderators).
    /// </summary>
    public string? PageUrl { get; init; }

    /// <summary>
    /// Active UI language, so moderators know which language to answer in.
    /// </summary>
    public string? Language { get; init; }
}
