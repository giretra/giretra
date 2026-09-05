namespace Giretra.Web.Services.Email;

/// <summary>
/// Writes the message to the log instead of sending it. Used in offline mode (where it
/// counts as delivered, so the form can be exercised locally) and as the disabled
/// placeholder when no SMTP server is configured online.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, bool isEnabled)
    {
        _logger = logger;
        IsEnabled = isEnabled;
    }

    public bool IsEnabled { get; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "E-mail (not sent, no SMTP configured)\nTo: {To}\nReply-To: {ReplyTo}\nSubject: {Subject}\n\n{Body}",
            string.Join(", ", message.To), message.ReplyTo ?? "-", message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
