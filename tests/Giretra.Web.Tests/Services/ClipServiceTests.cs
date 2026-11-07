using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Endpoints;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;
using Giretra.Web.Services.Replay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using CorePosition = Giretra.Core.Players.PlayerPosition;

namespace Giretra.Web.Tests.Services;

public sealed class ClipServiceTests : IDisposable
{
    private static readonly PublicOrigin Origin = new("https://play.example.test");

    private readonly GiretraDbContext _db;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly ClipOptions _options = new() { RootPath = Path.Combine(Path.GetTempPath(), "giretra-clip-tests", Guid.NewGuid().ToString("N")), MaxPerDay = 3 };
    private readonly ClipService _service;
    private readonly DealReplayService _replays;

    public ClipServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GiretraDbContext(dbOptions);
        _replays = new DealReplayService(_db, NullLogger<DealReplayService>.Instance);
        var options = Options.Create(_options);
        _service = new ClipService(_db, _replays, new ClipThrottle(options, _time), new ClipStorage(options), NullLogger<ClipService>.Instance, _time);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_options.RootPath)) Directory.Delete(_options.RootPath, recursive: true);
    }

    [Fact]
    public async Task Create_QueuesClip_WithShortIdTitleAndUrls()
    {
        var (userId, player, match) = await SeedMatchAsync();

        var result = await _service.CreateAsync(userId, Request(match.Id, from: 2, to: 4, locale: "fr"), Origin);

        Assert.Equal(ClipCreateStatus.Created, result.Status);
        var clip = result.Clip!;
        Assert.Equal(10, clip.ShortId.Length);
        Assert.Equal(ClipStatusResponse.Pending, clip.Status);
        Assert.Equal(CorePosition.Bottom, clip.ViewPosition);  // defaults to the creator's seat
        Assert.Equal("fr", clip.Locale);
        Assert.Equal($"https://play.example.test/c/{clip.ShortId}", clip.PageUrl);
        Assert.Null(clip.PosterUrl);
        Assert.Null(clip.VideoUrl);
        Assert.Contains("Tout Atout", clip.Title);
        Assert.Contains("Donne 1, plis 2–4", clip.Title);
        Assert.Contains("Alice & Bob contre Rakoto & Rasoa", clip.Title);

        var stored = await _db.Clips.SingleAsync();
        Assert.Equal(player.Id, stored.CreatedByPlayerId);
        Assert.Equal(ClipStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task Create_SameParameters_ReturnsExistingClip()
    {
        var (userId, _, match) = await SeedMatchAsync();

        var first = await _service.CreateAsync(userId, Request(match.Id, 1, 8), Origin);
        var second = await _service.CreateAsync(userId, Request(match.Id, 1, 8), Origin);

        Assert.Equal(ClipCreateStatus.Created, first.Status);
        Assert.Equal(ClipCreateStatus.Existing, second.Status);
        Assert.Equal(first.Clip!.ShortId, second.Clip!.ShortId);
        Assert.Equal(1, await _db.Clips.CountAsync());
    }

    [Fact]
    public async Task Create_RejectsBadRangeNonParticipantAndAbandonedMatch()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var (strangerId, _) = AddHuman("Mallory");
        var abandoned = AddMatch(abandoned: true);
        AddSeat(abandoned, (await _db.Players.FirstAsync(p => p.UserId == userId)), PlayerPosition.Bottom, Team.Team1);
        AddDeal(abandoned, 1);
        await _db.SaveChangesAsync();

        Assert.Equal(ClipCreateStatus.InvalidRequest, (await _service.CreateAsync(userId, Request(match.Id, 0, 3), Origin)).Status);
        Assert.Equal(ClipCreateStatus.InvalidRequest, (await _service.CreateAsync(userId, Request(match.Id, 5, 3), Origin)).Status);
        Assert.Equal(ClipCreateStatus.InvalidRequest, (await _service.CreateAsync(userId, Request(match.Id, 1, 9), Origin)).Status);
        Assert.Equal(ClipCreateStatus.Forbidden, (await _service.CreateAsync(strangerId, Request(match.Id, 1, 2), Origin)).Status);
        Assert.Equal(ClipCreateStatus.MatchNotFound, (await _service.CreateAsync(userId, Request(Guid.NewGuid(), 1, 2), Origin)).Status);
        Assert.Equal(ClipCreateStatus.NotReplayable, (await _service.CreateAsync(userId, Request(abandoned.Id, 1, 2), Origin)).Status);
        Assert.Equal(0, await _db.Clips.CountAsync());
    }

    [Fact]
    public async Task Create_IsThrottled_ButDedupeIsNot()
    {
        var (userId, _, match) = await SeedMatchAsync();

        for (var i = 1; i <= 3; i++)
            Assert.Equal(ClipCreateStatus.Created, (await _service.CreateAsync(userId, Request(match.Id, i, i), Origin)).Status);

        Assert.Equal(ClipCreateStatus.Throttled, (await _service.CreateAsync(userId, Request(match.Id, 4, 4), Origin)).Status);
        // An identical clip is served from the existing row, not counted.
        Assert.Equal(ClipCreateStatus.Existing, (await _service.CreateAsync(userId, Request(match.Id, 1, 1), Origin)).Status);

        _time.Advance(TimeSpan.FromHours(25));
        Assert.Equal(ClipCreateStatus.Created, (await _service.CreateAsync(userId, Request(match.Id, 4, 4), Origin)).Status);
    }

    [Fact]
    public async Task GetPublic_IncludesReplay_AndHidesOptedOutNames()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var bob = await _db.Players.FirstAsync(p => p.User!.DisplayName == "Bob");
        bob.AppearInSharedClips = false;
        await _db.SaveChangesAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 2, locale: "mg"), Origin);

        var clip = await _service.GetPublicAsync(created.Clip!.ShortId, Origin);

        Assert.NotNull(clip);
        Assert.NotNull(clip!.Replay);
        var players = clip.Replay!.Players.ToDictionary(p => p.Position);
        Assert.Equal("Alice", players[CorePosition.Bottom].DisplayName);
        Assert.False(players[CorePosition.Bottom].NameHidden);
        Assert.Equal("Mpilalao N", players[CorePosition.Top].DisplayName);
        Assert.True(players[CorePosition.Top].NameHidden);
        Assert.Null(players[CorePosition.Top].PlayerId);
        Assert.Equal("Rakoto", players[CorePosition.Left].DisplayName);  // bots are always named
        Assert.Contains("Alice & Mpilalao N", clip.Title);
        Assert.Equal(8, clip.Replay.Tricks.Count);

        // The participant endpoint still shows real names.
        var mine = await _replays.GetReplayAsync(match.Id, 1, userId);
        Assert.Equal("Bob", mine.Replay!.Players.Single(p => p.Position == CorePosition.Top).DisplayName);
    }

    [Fact]
    public async Task GetPublic_UnknownOrMalformedId_IsNull()
    {
        Assert.Null(await _service.GetPublicAsync("nope", Origin));
        Assert.Null(await _service.GetPublicAsync("ABCDEFGHIJ", Origin));
        Assert.Null(await _service.GetPublicAsync("../../etc/", Origin));
    }

    [Fact]
    public async Task Delete_CreatorOrModeratorOnly_MarksRemovedAndHidesFromLists()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var (otherId, _) = AddHuman("Carol");
        await _db.SaveChangesAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 1), Origin);
        var shortId = created.Clip!.ShortId;

        Assert.Equal(ClipDeleteStatus.Forbidden, await _service.DeleteAsync(otherId, shortId, isModerator: false));
        Assert.Single((await _service.ListMineAsync(userId, Origin)).Items);

        Assert.Equal(ClipDeleteStatus.Ok, await _service.DeleteAsync(otherId, shortId, isModerator: true));
        Assert.Equal(ClipStatus.Removed, (await _db.Clips.SingleAsync()).Status);
        Assert.Empty((await _service.ListMineAsync(userId, Origin)).Items);

        var publicView = await _service.GetPublicAsync(shortId, Origin);
        Assert.Equal(ClipStatusResponse.Removed, publicView!.Status);
        Assert.Null(publicView.Replay);
        Assert.Equal(ClipDeleteStatus.NotFound, await _service.DeleteAsync(userId, shortId, isModerator: false));
    }

    [Fact]
    public async Task ReadyClip_ExposesPosterAndVideoUrls()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 1), Origin);
        var clip = await _db.Clips.SingleAsync();
        clip.Status = ClipStatus.Ready;
        clip.HasPoster = true;
        clip.HasVideo = true;
        clip.DurationMs = 12345;
        await _db.SaveChangesAsync();

        var view = await _service.GetPublicAsync(created.Clip!.ShortId, Origin);

        Assert.Equal($"https://play.example.test/c/{clip.ShortId}/poster.png", view!.PosterUrl);
        Assert.Equal($"https://play.example.test/c/{clip.ShortId}/video.mp4", view.VideoUrl);
        Assert.Equal(12345, view.DurationMs);
    }

    #region Landing page

    [Fact]
    public async Task LandingPage_Ready_HasOpenGraphVideoTags_AndEscapesNames()
    {
        var (userId, _, match) = await SeedMatchAsync(aliceName: "Al<script>ice");
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 3), Origin);
        var clip = await _db.Clips.SingleAsync();
        clip.Status = ClipStatus.Ready;
        clip.HasPoster = true;
        clip.HasVideo = true;
        clip.DurationMs = 20100;
        await _db.SaveChangesAsync();
        var view = (await _service.GetPublicAsync(created.Clip!.ShortId, Origin))!;

        var html = ClipLandingPage.Render(view, Origin);

        Assert.DoesNotContain("<script>ice", html);
        Assert.Contains("Al&lt;script&gt;ice", html);
        Assert.Contains("<meta property=\"og:type\" content=\"video.other\">", html);
        Assert.Contains($"<meta property=\"og:video:secure_url\" content=\"https://play.example.test/c/{clip.ShortId}/video.mp4\">", html);
        Assert.Contains("<meta property=\"og:video:type\" content=\"video/mp4\">", html);
        Assert.Contains("<meta property=\"og:video:width\" content=\"1080\">", html);
        Assert.Contains($"<meta property=\"og:image\" content=\"https://play.example.test/c/{clip.ShortId}/poster.png\">", html);
        Assert.Contains("<meta property=\"video:duration\" content=\"21\">", html);
        Assert.Contains("<video", html);
        Assert.Contains("?download=1", html);
        Assert.Contains($"/replay/clip/{clip.ShortId}", html);
        Assert.DoesNotContain("noindex", html);
        Assert.DoesNotContain("http-equiv=\"refresh\"", html);
    }

    [Fact]
    public async Task LandingPage_Pending_IsNoIndexAndRefreshes_WithGenericImage()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 3), Origin);
        var view = (await _service.GetPublicAsync(created.Clip!.ShortId, Origin))!;

        var html = ClipLandingPage.Render(view, Origin);

        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
        Assert.Contains("http-equiv=\"refresh\"", html);
        Assert.Contains("<meta property=\"og:image\" content=\"https://play.example.test/og-image.png\">", html);
        Assert.DoesNotContain("og:video", html);
        Assert.DoesNotContain("<video", html);
    }

    [Fact]
    public async Task LandingPage_Removed_SaysSo()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 3), Origin);
        await _service.DeleteAsync(userId, created.Clip!.ShortId, isModerator: false);
        var view = (await _service.GetPublicAsync(created.Clip.ShortId, Origin))!;

        var html = ClipLandingPage.Render(view, Origin);

        Assert.Contains("no longer available", html);
        Assert.DoesNotContain("<video", html);
    }

    [Fact]
    public async Task LandingPage_Failed_ExplainsAndDoesNotRefresh()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 3), Origin);
        var row = await _db.Clips.FirstAsync(c => c.ShortId == created.Clip!.ShortId);
        row.Status = ClipStatus.Failed;
        row.Error = "ffmpeg exited with 1";
        await _db.SaveChangesAsync();
        var view = (await _service.GetPublicAsync(created.Clip!.ShortId, Origin))!;

        var html = ClipLandingPage.Render(view, Origin);

        Assert.Contains("could not be generated", html);
        Assert.DoesNotContain("http-equiv=\"refresh\"", html);
        Assert.Contains("noindex", html);
        Assert.DoesNotContain("ffmpeg", html);
    }

    #endregion

    #region Retention and moderation

    [Fact]
    public async Task GetPublic_RequeuesAnEvictedClip()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var created = await _service.CreateAsync(userId, Request(match.Id, 1, 3), Origin);
        var row = await _db.Clips.FirstAsync(c => c.ShortId == created.Clip!.ShortId);
        row.Status = ClipStatus.Evicted;
        row.Attempts = 2;
        row.Error = "old";
        await _db.SaveChangesAsync();

        var view = (await _service.GetPublicAsync(created.Clip!.ShortId, Origin))!;

        Assert.Equal(ClipStatusResponse.Pending, view.Status);
        Assert.Null(view.VideoUrl);
        var after = await _db.Clips.AsNoTracking().FirstAsync(c => c.Id == row.Id);
        Assert.Equal(ClipStatus.Pending, after.Status);
        Assert.Equal(0, after.Attempts);
        Assert.Null(after.Error);
    }

    [Fact]
    public async Task Admin_ListsWithDiagnostics_FiltersByStatus_AndRetries()
    {
        var (userId, _, match) = await SeedMatchAsync();
        var ok = await _service.CreateAsync(userId, Request(match.Id, 1, 2), Origin);
        var bad = await _service.CreateAsync(userId, Request(match.Id, 3, 4), Origin);
        var row = await _db.Clips.FirstAsync(c => c.ShortId == bad.Clip!.ShortId);
        row.Status = ClipStatus.Failed;
        row.Attempts = 2;
        row.Error = "Render timed out";
        await _db.SaveChangesAsync();

        var all = await _service.ListForAdminAsync(null, 1, 25, Origin);
        var failed = await _service.ListForAdminAsync(ClipStatusResponse.Failed, 1, 25, Origin);

        Assert.Equal(2, all.TotalCount);
        Assert.Single(failed.Items);
        var entry = failed.Items[0];
        Assert.Equal(bad.Clip!.ShortId, entry.Clip.ShortId);
        Assert.Equal("Render timed out", entry.Error);
        Assert.Equal(2, entry.Attempts);
        Assert.Equal("Alice", entry.CreatedByName);
        Assert.Null(entry.Clip.Replay);

        Assert.True(await _service.RetryAsync(bad.Clip.ShortId));
        var after = await _db.Clips.AsNoTracking().FirstAsync(c => c.Id == row.Id);
        Assert.Equal(ClipStatus.Pending, after.Status);
        Assert.Equal(0, after.Attempts);
        Assert.Null(after.Error);

        await _service.DeleteAsync(userId, ok.Clip!.ShortId, isModerator: true);
        Assert.False(await _service.RetryAsync(ok.Clip.ShortId));
        Assert.False(await _service.RetryAsync("nope"));
    }

    #endregion

    #region Fixture

    private static CreateClipRequest Request(Guid matchId, int from, int to, string locale = "en", CorePosition? view = null, bool allHands = false) => new()
    {
        MatchId = matchId,
        DealNumber = 1,
        FromTrick = from,
        ToTrick = to,
        Locale = locale,
        ViewPosition = view,
        ShowAllHands = allHands,
    };

    private async Task<(Guid UserId, Player Player, Match Match)> SeedMatchAsync(string aliceName = "Alice")
    {
        var (userId, alice) = AddHuman(aliceName);
        var match = AddMatch(abandoned: false);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        AddSeat(match, AddHuman("Bob").Player, PlayerPosition.Top, Team.Team1);
        AddSeat(match, AddBot("Rakoto"), PlayerPosition.Left, Team.Team2);
        AddSeat(match, AddBot("Rasoa"), PlayerPosition.Right, Team.Team2);
        AddDeal(match, 1);
        await _db.SaveChangesAsync();
        return (userId, alice, match);
    }

    private (Guid UserId, Player Player) AddHuman(string name)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString("N"),
            DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var player = new Player { Id = Guid.NewGuid(), PlayerType = PlayerType.Human, UserId = user.Id, CreatedAt = DateTimeOffset.UtcNow };
        _db.Users.Add(user);
        _db.Players.Add(player);
        return (user.Id, player);
    }

    private Player AddBot(string name)
    {
        var bot = new Bot { Id = Guid.NewGuid(), AgentType = "Test", AgentTypeFactory = "Test", DisplayName = name, CreatedAt = DateTimeOffset.UtcNow };
        var player = new Player { Id = Guid.NewGuid(), PlayerType = PlayerType.Bot, BotId = bot.Id, CreatedAt = DateTimeOffset.UtcNow };
        _db.Bots.Add(bot);
        _db.Players.Add(player);
        return player;
    }

    private Match AddMatch(bool abandoned)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoomName = "Room",
            TargetScore = 150,
            TotalDeals = 1,
            IsRanked = true,
            WasAbandoned = abandoned,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        _db.Matches.Add(match);
        return match;
    }

    private void AddSeat(Match match, Player player, PlayerPosition position, Team team)
        => _db.MatchPlayers.Add(new MatchPlayer { Id = Guid.NewGuid(), MatchId = match.Id, PlayerId = player.Id, Position = position, Team = team });

    /// <summary>Same oracle deal as <see cref="DealReplayServiceTests"/>: All Trumps, 32 plays.</summary>
    private void AddDeal(Match match, int number)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DealNumber = (short)number,
            DealerPosition = PlayerPosition.Right,
            GameMode = GameMode.AllTrumps,
            AnnouncerTeam = Team.Team1,
            Multiplier = MultiplierState.Normal,
            Team1CardPoints = 227,
            Team2CardPoints = 31,
            Team1MatchPoints = 2,
            Team2MatchPoints = 0,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow,
        };
        var b = PlayerPosition.Bottom; var l = PlayerPosition.Left; var t = PlayerPosition.Top; var r = PlayerPosition.Right;
        (PlayerPosition P, CardRank R, CardSuit S)[] plays =
        [
            (b, CardRank.Jack, CardSuit.Spades), (l, CardRank.Nine, CardSuit.Spades), (t, CardRank.Ace, CardSuit.Spades), (r, CardRank.Ten, CardSuit.Spades),
            (b, CardRank.Seven, CardSuit.Spades), (l, CardRank.King, CardSuit.Spades), (t, CardRank.Queen, CardSuit.Spades), (r, CardRank.Eight, CardSuit.Spades),
            (l, CardRank.Nine, CardSuit.Hearts), (t, CardRank.Ace, CardSuit.Hearts), (r, CardRank.Ten, CardSuit.Hearts), (b, CardRank.Jack, CardSuit.Hearts),
            (b, CardRank.Eight, CardSuit.Hearts), (l, CardRank.Seven, CardSuit.Hearts), (t, CardRank.King, CardSuit.Hearts), (r, CardRank.Queen, CardSuit.Hearts),
            (t, CardRank.Ace, CardSuit.Diamonds), (r, CardRank.Ten, CardSuit.Diamonds), (b, CardRank.Jack, CardSuit.Diamonds), (l, CardRank.Nine, CardSuit.Diamonds),
            (b, CardRank.Queen, CardSuit.Diamonds), (l, CardRank.Eight, CardSuit.Diamonds), (t, CardRank.Seven, CardSuit.Diamonds), (r, CardRank.King, CardSuit.Diamonds),
            (r, CardRank.Ten, CardSuit.Clubs), (b, CardRank.Jack, CardSuit.Clubs), (l, CardRank.Nine, CardSuit.Clubs), (t, CardRank.Ace, CardSuit.Clubs),
            (b, CardRank.Seven, CardSuit.Clubs), (l, CardRank.King, CardSuit.Clubs), (t, CardRank.Queen, CardSuit.Clubs), (r, CardRank.Eight, CardSuit.Clubs),
        ];
        for (var i = 0; i < plays.Length; i++)
        {
            deal.DealActions.Add(new DealAction
            {
                Id = Guid.NewGuid(), DealId = deal.Id, ActionOrder = (short)(i + 1), ActionType = ActionType.PlayCard,
                PlayerPosition = plays[i].P, CardRank = plays[i].R, CardSuit = plays[i].S, TrickNumber = (short)(i / 4 + 1),
            });
        }
        _db.Deals.Add(deal);
    }

    #endregion
}
