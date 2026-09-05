using System.Text;
using Giretra.Model.Entities;
using Giretra.Web.Models.Requests;
using Giretra.Web.Services.Email;

namespace Giretra.Web.Services;

/// <summary>
/// Validates a contact-form submission and turns it into a plain-text e-mail.
/// </summary>
public static class FeedbackMailComposer
{
    public const int SubjectMinLength = 3;
    public const int SubjectMaxLength = 120;
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 4000;
    private const int PageUrlMaxLength = 500;

    public static (bool IsValid, string? Error) Validate(SendFeedbackRequest request)
    {
        if (!Enum.IsDefined(request.Category))
            return (false, "Unknown category.");

        var subject = request.Subject?.Trim() ?? string.Empty;
        if (subject.Length < SubjectMinLength)
            return (false, $"Subject must be at least {SubjectMinLength} characters.");
        if (subject.Length > SubjectMaxLength)
            return (false, $"Subject must be at most {SubjectMaxLength} characters.");
        if (subject.Any(c => c is '\r' or '\n'))
            return (false, "Subject cannot span several lines.");

        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length < MessageMinLength)
            return (false, $"Message must be at least {MessageMinLength} characters.");
        if (message.Length > MessageMaxLength)
            return (false, $"Message must be at most {MessageMaxLength} characters.");

        if (request.PageUrl is { Length: > PageUrlMaxLength })
            return (false, "Page URL is too long.");

        return (true, null);
    }

    public static EmailMessage Compose(
        User sender,
        SendFeedbackRequest request,
        FeedbackContext context,
        IReadOnlyList<string> recipients,
        DateTimeOffset sentAt)
    {
        var subject = $"[Giretra] {CategoryLabel(request.Category)}: {request.Subject.Trim()}";

        var body = new StringBuilder();
        body.AppendLine(request.Message.Trim());
        body.AppendLine();
        body.AppendLine("----------------------------------------");
        body.AppendLine($"Category:   {CategoryLabel(request.Category)}");
        body.AppendLine($"From:       {sender.EffectiveDisplayName} (@{sender.Username})");
        body.AppendLine($"E-mail:     {(string.IsNullOrWhiteSpace(sender.Email) ? "not available" : sender.Email)}");
        body.AppendLine($"User id:    {sender.Id}");
        body.AppendLine($"Sent:       {sentAt:yyyy-MM-dd HH:mm} UTC");
        if (!string.IsNullOrWhiteSpace(request.Language))
            body.AppendLine($"Language:   {request.Language.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.PageUrl))
            body.AppendLine($"Page:       {SingleLine(request.PageUrl)}");
        if (!string.IsNullOrWhiteSpace(context.UserAgent))
            body.AppendLine($"Browser:    {SingleLine(context.UserAgent)}");
        body.AppendLine();
        body.AppendLine("Sent from the Giretra in-app contact form. Reply to this e-mail to answer the player.");

        var replyTo = string.IsNullOrWhiteSpace(sender.Email) ? null : sender.Email;
        return new EmailMessage(recipients, subject, body.ToString(), replyTo, replyTo is null ? null : sender.EffectiveDisplayName);
    }

    public static string CategoryLabel(FeedbackCategory category) => category switch
    {
        FeedbackCategory.Bug => "Bug",
        FeedbackCategory.Idea => "Idea",
        FeedbackCategory.Question => "Question",
        _ => "Message"
    };

    private static string SingleLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
