using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models;
using Giretra.Web.Models.Requests;
using Giretra.Web.Services;
using Giretra.Web.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Giretra.Web.Tests.Services;

public sealed class FeedbackServiceTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IModeratorDirectory _moderators = Substitute.For<IModeratorDirectory>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero));
    private readonly FeedbackOptions _options = new();
    private readonly FeedbackContext _context = new("TestBrowser/1.0");

    private static readonly User Sender = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Username = "rakoto",
        DisplayName = "Rakoto",
        CustomDisplayName = "Rakoto le Fort",
        Email = "rakoto@example.com",
        Role = UserRole.Normal,
    };

    public FeedbackServiceTests()
    {
        _emailSender.IsEnabled.Returns(true);
        _moderators.GetModeratorEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "mod@giretra.com" });
    }

    private FeedbackService CreateService(FeedbackThrottle? throttle = null) =>
        new(_emailSender, _moderators, throttle ?? new FeedbackThrottle(_time), Options.Create(_options),
            NullLogger<FeedbackService>.Instance, _time);

    private static SendFeedbackRequest ValidRequest(FeedbackCategory category = FeedbackCategory.Bug) => new()
    {
        Category = category,
        Subject = "Cards overlap on small screens",
        Message = "When I play on my phone the third card is hidden behind the score bar.",
        PageUrl = "/table/abc",
        Language = "mg",
    };

    [Fact]
    public async Task Send_DeliversToModeratorsAndExtraRecipients_Deduplicated()
    {
        _options.ExtraRecipients = ["Mod@giretra.com", "owner@example.com", " owner@example.com "];

        var result = await CreateService().SendAsync(Sender, ValidRequest(), _context);

        Assert.Equal(FeedbackOutcome.Sent, result.Outcome);
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To.Count == 2 &&
                m.To.Contains("mod@giretra.com") &&
                m.To.Contains("owner@example.com")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_ComposedMailCarriesSenderIdentityAndReplyTo()
    {
        EmailMessage? sent = null;
        await _emailSender.SendAsync(Arg.Do<EmailMessage>(m => sent = m), Arg.Any<CancellationToken>());

        await CreateService().SendAsync(Sender, ValidRequest(FeedbackCategory.Idea), _context);

        Assert.NotNull(sent);
        Assert.Equal("[Giretra] Idea: Cards overlap on small screens", sent.Subject);
        Assert.Equal("rakoto@example.com", sent.ReplyTo);
        Assert.Equal("Rakoto le Fort", sent.ReplyToName);
        Assert.Contains("third card is hidden", sent.TextBody);
        Assert.Contains("Rakoto le Fort (@rakoto)", sent.TextBody);
        Assert.Contains(Sender.Id.ToString(), sent.TextBody);
        Assert.Contains("Page:       /table/abc", sent.TextBody);
        Assert.Contains("Browser:    TestBrowser/1.0", sent.TextBody);
        Assert.Contains("Language:   mg", sent.TextBody);
        Assert.Contains("2026-09-05 10:00 UTC", sent.TextBody);
    }

    [Fact]
    public async Task Send_WithoutSenderEmail_HasNoReplyTo()
    {
        EmailMessage? sent = null;
        await _emailSender.SendAsync(Arg.Do<EmailMessage>(m => sent = m), Arg.Any<CancellationToken>());
        var anonymous = new User { Id = Guid.NewGuid(), Username = "ghost", DisplayName = "Ghost", Email = null };

        await CreateService().SendAsync(anonymous, ValidRequest(), _context);

        Assert.NotNull(sent);
        Assert.Null(sent.ReplyTo);
        Assert.Contains("E-mail:     not available", sent.TextBody);
    }

    [Theory]
    [InlineData("", "Long enough message body here.")]
    [InlineData("ab", "Long enough message body here.")]
    [InlineData("Subject", "short")]
    [InlineData("Line\nbreak", "Long enough message body here.")]
    public async Task Send_RejectsInvalidInput_WithoutSending(string subject, string message)
    {
        var request = new SendFeedbackRequest { Category = FeedbackCategory.Other, Subject = subject, Message = message };

        var result = await CreateService().SendAsync(Sender, request, _context);

        Assert.Equal(FeedbackOutcome.Invalid, result.Outcome);
        Assert.NotNull(result.Error);
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_RejectsOversizedMessage()
    {
        var request = new SendFeedbackRequest
        {
            Category = FeedbackCategory.Bug,
            Subject = "Too long",
            Message = new string('x', FeedbackMailComposer.MessageMaxLength + 1),
        };

        var result = await CreateService().SendAsync(Sender, request, _context);

        Assert.Equal(FeedbackOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Send_WhenTransportDisabled_ReportsNotConfigured()
    {
        _emailSender.IsEnabled.Returns(false);

        var result = await CreateService().SendAsync(Sender, ValidRequest(), _context);

        Assert.Equal(FeedbackOutcome.NotConfigured, result.Outcome);
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenNobodyToReceive_ReportsNotConfigured()
    {
        _moderators.GetModeratorEmailsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());
        _options.ExtraRecipients = ["not-an-address"];

        var result = await CreateService().SendAsync(Sender, ValidRequest(), _context);

        Assert.Equal(FeedbackOutcome.NotConfigured, result.Outcome);
    }

    [Fact]
    public async Task Send_WhenTransportThrows_ReportsFailed()
    {
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("connection refused"));

        var result = await CreateService().SendAsync(Sender, ValidRequest(), _context);

        Assert.Equal(FeedbackOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Send_IsRateLimitedPerUser()
    {
        var service = CreateService();

        var first = await service.SendAsync(Sender, ValidRequest(), _context);
        var immediateRetry = await service.SendAsync(Sender, ValidRequest(), _context);

        Assert.Equal(FeedbackOutcome.Sent, first.Outcome);
        Assert.Equal(FeedbackOutcome.RateLimited, immediateRetry.Outcome);

        var other = new User { Id = Guid.NewGuid(), Username = "other", DisplayName = "Other", Email = "o@example.com" };
        var otherUser = await service.SendAsync(other, ValidRequest(), _context);
        Assert.Equal(FeedbackOutcome.Sent, otherUser.Outcome);

        _time.Advance(FeedbackThrottle.MinInterval);
        var afterPause = await service.SendAsync(Sender, ValidRequest(), _context);
        Assert.Equal(FeedbackOutcome.Sent, afterPause.Outcome);
    }

    [Fact]
    public async Task Send_CapsMessagesPerHour()
    {
        var service = CreateService();

        for (var i = 0; i < FeedbackThrottle.MaxPerWindow; i++)
        {
            var ok = await service.SendAsync(Sender, ValidRequest(), _context);
            Assert.Equal(FeedbackOutcome.Sent, ok.Outcome);
            _time.Advance(FeedbackThrottle.MinInterval);
        }

        var capped = await service.SendAsync(Sender, ValidRequest(), _context);
        Assert.Equal(FeedbackOutcome.RateLimited, capped.Outcome);

        _time.Advance(FeedbackThrottle.Window);
        var nextHour = await service.SendAsync(Sender, ValidRequest(), _context);
        Assert.Equal(FeedbackOutcome.Sent, nextHour.Outcome);
    }

    [Fact]
    public async Task GetConfig_ContactEnabled_OnlyWithTransportAndRecipients()
    {
        _options.GitHubIssuesUrl = "https://github.com/example/issues";

        var enabled = await CreateService().GetConfigAsync();
        Assert.True(enabled.ContactEnabled);
        Assert.Equal("https://github.com/example/issues", enabled.GitHubIssuesUrl);

        _moderators.GetModeratorEmailsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());
        var noRecipients = await CreateService().GetConfigAsync();
        Assert.False(noRecipients.ContactEnabled);

        _options.ExtraRecipients = ["owner@example.com"];
        var extraOnly = await CreateService().GetConfigAsync();
        Assert.True(extraOnly.ContactEnabled);

        _emailSender.IsEnabled.Returns(false);
        var noTransport = await CreateService().GetConfigAsync();
        Assert.False(noTransport.ContactEnabled);
        await _moderators.Received(3).GetModeratorEmailsAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class FeedbackOptionsTests
{
    [Fact]
    public void ApplyEnvironmentOverrides_ReadsSmtpAndRecipients()
    {
        var env = new Dictionary<string, string?>
        {
            ["Giretra_Smtp_Host"] = " smtp.example.com ",
            ["Giretra_Smtp_Port"] = "465",
            ["Giretra_Smtp_User"] = "mailer",
            ["Giretra_Smtp_Password"] = "s3cret",
            ["Giretra_Smtp_From"] = "noreply@giretra.com",
            ["Giretra_Feedback_ExtraRecipients"] = "a@example.com; b@example.com,c@example.com",
        };
        var options = new FeedbackOptions();

        options.ApplyEnvironmentOverrides(k => env.GetValueOrDefault(k));

        Assert.Equal("smtp.example.com", options.Smtp.Host);
        Assert.Equal(465, options.Smtp.Port);
        Assert.Equal("mailer", options.Smtp.User);
        Assert.Equal("s3cret", options.Smtp.Password);
        Assert.Equal("noreply@giretra.com", options.Smtp.From);
        Assert.True(options.Smtp.IsConfigured);
        Assert.Equal(["a@example.com", "b@example.com", "c@example.com"], options.ExtraRecipients);
    }

    [Fact]
    public void ApplyEnvironmentOverrides_KeepsAppsettingsValues_WhenVariablesMissing()
    {
        var options = new FeedbackOptions
        {
            ExtraRecipients = ["from-appsettings@example.com"],
            Smtp = new SmtpOptions { Host = "mail.internal", User = "svc" },
        };

        options.ApplyEnvironmentOverrides(_ => null);

        Assert.Equal("mail.internal", options.Smtp.Host);
        Assert.Equal(587, options.Smtp.Port);
        Assert.Equal("svc", options.Smtp.EffectiveFrom);
        Assert.Equal(["from-appsettings@example.com"], options.ExtraRecipients);
    }

    [Fact]
    public void Smtp_IsNotConfigured_WithoutHostOrSender()
    {
        Assert.False(new SmtpOptions().IsConfigured);
        Assert.False(new SmtpOptions { Host = "mail.internal" }.IsConfigured);
        Assert.True(new SmtpOptions { Host = "mail.internal", From = "noreply@giretra.com" }.IsConfigured);
    }
}

public sealed class DbModeratorDirectoryTests : IDisposable
{
    private readonly GiretraDbContext _db;

    public DbModeratorDirectoryTests()
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GiretraDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ReturnsEmailsOfActiveStaffOnly()
    {
        _db.Users.AddRange(
            NewUser("mod", UserRole.Moderator, "mod@giretra.com"),
            NewUser("admin", UserRole.Admin, "admin@giretra.com"),
            NewUser("banned-mod", UserRole.Moderator, "banned@giretra.com", banned: true),
            NewUser("mod-no-mail", UserRole.Moderator, null),
            NewUser("player", UserRole.Normal, "player@example.com"));
        await _db.SaveChangesAsync();

        var emails = await new DbModeratorDirectory(_db).GetModeratorEmailsAsync();

        Assert.Equal(["admin@giretra.com", "mod@giretra.com"], emails.OrderBy(e => e).ToList());
    }

    private static User NewUser(string username, UserRole role, string? email, bool banned = false) => new()
    {
        Id = Guid.NewGuid(),
        KeycloakId = Guid.NewGuid(),
        Username = username,
        DisplayName = username,
        Email = email,
        Role = role,
        IsBanned = banned,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
