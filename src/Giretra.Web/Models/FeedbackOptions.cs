namespace Giretra.Web.Models;

/// <summary>
/// Contact-form settings, bound from the "Feedback" configuration section and then
/// overridden by <c>Giretra_Smtp_*</c> / <c>Giretra_Feedback_*</c> environment variables
/// (the same convention as the database settings, so they fit in the root <c>.env</c>).
/// </summary>
public sealed class FeedbackOptions
{
    public const string SectionName = "Feedback";

    /// <summary>
    /// Where players who have a GitHub account can open an issue instead.
    /// </summary>
    public string GitHubIssuesUrl { get; set; } = "https://github.com/giretra/giretra/issues/new/choose";

    /// <summary>
    /// Addresses that receive every contact-form message in addition to the moderators.
    /// Comma- or semicolon-separated in the environment variable.
    /// </summary>
    public List<string> ExtraRecipients { get; set; } = [];

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>
    /// Overlays environment variables on top of whatever came from appsettings.
    /// </summary>
    public void ApplyEnvironmentOverrides(Func<string, string?> getVariable)
    {
        var host = getVariable("Giretra_Smtp_Host");
        if (!string.IsNullOrWhiteSpace(host))
            Smtp.Host = host.Trim();

        var port = getVariable("Giretra_Smtp_Port");
        if (int.TryParse(port, out var parsedPort) && parsedPort > 0)
            Smtp.Port = parsedPort;

        var user = getVariable("Giretra_Smtp_User");
        if (!string.IsNullOrWhiteSpace(user))
            Smtp.User = user.Trim();

        var password = getVariable("Giretra_Smtp_Password");
        if (!string.IsNullOrEmpty(password))
            Smtp.Password = password;

        var from = getVariable("Giretra_Smtp_From");
        if (!string.IsNullOrWhiteSpace(from))
            Smtp.From = from.Trim();

        var extra = getVariable("Giretra_Feedback_ExtraRecipients");
        if (!string.IsNullOrWhiteSpace(extra))
        {
            ExtraRecipients = extra
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }
}

/// <summary>
/// Outgoing mail server. Mail delivery is disabled while <see cref="Host"/> is empty.
/// </summary>
public sealed class SmtpOptions
{
    public string? Host { get; set; }

    /// <summary>
    /// 587 (STARTTLS) by default; 465 switches to implicit TLS.
    /// </summary>
    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// Sender address. Falls back to <see cref="User"/> when unset.
    /// </summary>
    public string? From { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(EffectiveFrom);

    public string? EffectiveFrom => string.IsNullOrWhiteSpace(From) ? User : From;
}
