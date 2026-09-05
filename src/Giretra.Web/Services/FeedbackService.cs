using Giretra.Model.Entities;
using Giretra.Web.Models;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Email;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services;

/// <summary>
/// Delivers contact-form messages to the moderators plus the configured extra recipients.
/// </summary>
public sealed class FeedbackService : IFeedbackService
{
    private readonly IEmailSender _emailSender;
    private readonly IModeratorDirectory _moderators;
    private readonly FeedbackThrottle _throttle;
    private readonly IOptions<FeedbackOptions> _options;
    private readonly ILogger<FeedbackService> _logger;
    private readonly TimeProvider _time;

    public FeedbackService(
        IEmailSender emailSender,
        IModeratorDirectory moderators,
        FeedbackThrottle throttle,
        IOptions<FeedbackOptions> options,
        ILogger<FeedbackService> logger,
        TimeProvider? time = null)
    {
        _emailSender = emailSender;
        _moderators = moderators;
        _throttle = throttle;
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<FeedbackConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var recipients = _emailSender.IsEnabled
            ? await ResolveRecipientsAsync(cancellationToken)
            : [];

        return new FeedbackConfigResponse
        {
            ContactEnabled = recipients.Count > 0,
            GitHubIssuesUrl = _options.Value.GitHubIssuesUrl
        };
    }

    public async Task<FeedbackResult> SendAsync(User sender, SendFeedbackRequest request, FeedbackContext context, CancellationToken cancellationToken = default)
    {
        var (isValid, error) = FeedbackMailComposer.Validate(request);
        if (!isValid)
            return new FeedbackResult(FeedbackOutcome.Invalid, error);

        if (!_emailSender.IsEnabled)
            return new FeedbackResult(FeedbackOutcome.NotConfigured, "The contact form is not available on this server.");

        var recipients = await ResolveRecipientsAsync(cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Contact form message from {UserId} dropped: no moderator has an e-mail and no extra recipient is configured", sender.Id);
            return new FeedbackResult(FeedbackOutcome.NotConfigured, "Nobody is configured to receive messages on this server.");
        }

        if (!_throttle.TryAcquire(sender.Id))
            return new FeedbackResult(FeedbackOutcome.RateLimited, "You have sent several messages recently. Please wait a moment before sending another.");

        var email = FeedbackMailComposer.Compose(sender, request, context, recipients, _time.GetUtcNow());

        try
        {
            await _emailSender.SendAsync(email, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to deliver contact form message from {UserId}", sender.Id);
            return new FeedbackResult(FeedbackOutcome.Failed, "The message could not be delivered. Please try again later.");
        }

        _logger.LogInformation("Contact form message ({Category}) from {UserId} delivered to {Count} recipient(s)",
            request.Category, sender.Id, recipients.Count);
        return FeedbackResult.Success;
    }

    /// <summary>
    /// Moderator addresses plus the configured extras, de-duplicated case-insensitively.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(CancellationToken cancellationToken)
    {
        var moderators = await _moderators.GetModeratorEmailsAsync(cancellationToken);

        return moderators
            .Concat(_options.Value.ExtraRecipients)
            .Select(e => e.Trim())
            .Where(e => e.Length > 0 && e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
