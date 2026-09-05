using Giretra.Web.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Giretra.Web.Services.Email;

/// <summary>
/// Sends mail through the SMTP server described by <see cref="SmtpOptions"/> using MailKit.
/// A fresh connection is opened per message; the contact form is low-volume.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<FeedbackOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<FeedbackOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.Value.Smtp.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var smtp = _options.Value.Smtp;
        if (!smtp.IsConfigured)
            throw new InvalidOperationException("SMTP is not configured.");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Giretra", smtp.EffectiveFrom));
        foreach (var to in message.To)
            mime.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            mime.ReplyTo.Add(new MailboxAddress(message.ReplyToName ?? message.ReplyTo, message.ReplyTo));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        // Auto: implicit TLS on 465, STARTTLS when the server offers it otherwise.
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrWhiteSpace(smtp.User))
            await client.AuthenticateAsync(smtp.User, smtp.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent e-mail \"{Subject}\" to {RecipientCount} recipient(s) via {Host}",
            message.Subject, message.To.Count, smtp.Host);
    }
}
