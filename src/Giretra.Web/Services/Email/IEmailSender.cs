namespace Giretra.Web.Services.Email;

/// <summary>
/// A plain-text e-mail ready to be handed to a transport.
/// </summary>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string TextBody,
    string? ReplyTo = null,
    string? ReplyToName = null);

/// <summary>
/// Outgoing mail transport. Implementations: SMTP (production) and a log-only sink (offline / unconfigured).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// False when messages cannot actually reach anyone (no transport configured).
    /// </summary>
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
