using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Tests.Services;

public sealed class HighlightsServiceTests : IDisposable
{
    private readonly GiretraDbContext _db;
    private readonly HighlightsService _service;

    public HighlightsServiceTests()
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new GiretraDbContext(options);
        _service = new HighlightsService(_db);
    }

    public void Dispose() => _db.Dispose();

    #region Empty states

    [Fact]
    public async Task NoPlayerRow_ReturnsEmptyShape()
    {
        var result = await _service.GetHighlightsAsync(Guid.NewGuid());

        Assert.Equal(0, result.Hero.GamesPlayed);
        Assert.Equal(6, result.ModeStats.Count);
        Assert.All(result.ModeStats, m => Assert.Equal(0, m.DealsPlayed));
        Assert.Empty(result.EloTrend);
        Assert.Empty(result.Callouts);
        Assert.Empty(result.Activity);
        Assert.Null(result.BestPartner);
        Assert.Null(result.Nemesis);
    }

    [Fact]
    public async Task PlayerWithoutMatches_ReturnsHeroFromPlayerRow()
    {
        var (userId, _) = AddHuman("Alice", elo: 1234, gamesPlayed: 0, gamesWon: 0);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(1234, result.Hero.EloRating);
        Assert.Equal(0, result.Hero.GamesPlayed);
        Assert.Empty(result.Hero.RecentForm);
        Assert.Equal(6, result.ModeStats.Count);
    }

    #endregion

    #region Mode stats

    [Fact]
    public async Task ModeStats_AggregatesDealsFromBothTeamSides()
    {
        var (userId, player) = AddHuman("Alice");

        // On Team1: wins a Hearts deal 2-0, loses a Hearts deal 0-2
        var m1 = AddMatch(DaysAgo(2));
        AddMatchPlayer(m1, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        AddDeal(m1, 1, GameMode.ColourHearts, Team.Team1, announcerWon: true, t1MatchPoints: 2, t2MatchPoints: 0, t1CardPoints: 90, t2CardPoints: 30);
        AddDeal(m1, 2, GameMode.ColourHearts, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 2, t1CardPoints: 40, t2CardPoints: 80);

        // On Team2: wins an AllTrumps deal
        var m2 = AddMatch(DaysAgo(1));
        AddMatchPlayer(m2, player, PlayerPosition.Left, Team.Team2, isWinner: false);
        AddDeal(m2, 1, GameMode.AllTrumps, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 3, t1CardPoints: 50, t2CardPoints: 200);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(6, result.ModeStats.Count);
        var hearts = result.ModeStats.Single(m => m.Mode == GameMode.ColourHearts);
        Assert.Equal(2, hearts.DealsPlayed);
        Assert.Equal(1, hearts.DealsWon);
        Assert.Equal(50, hearts.DealWinRate);
        Assert.Equal(65, hearts.AvgCardPoints); // (90 + 40) / 2 as Team1

        var allTrumps = result.ModeStats.Single(m => m.Mode == GameMode.AllTrumps);
        Assert.Equal(1, allTrumps.DealsPlayed);
        Assert.Equal(1, allTrumps.DealsWon);
        Assert.Equal(200, allTrumps.AvgCardPoints); // Team2 side

        Assert.Equal(0, result.ModeStats.Single(m => m.Mode == GameMode.NoTrumps).DealsPlayed);
        Assert.Equal(3, result.Bidding.DealsPlayed);
    }

    #endregion

    #region Announcer attribution

    [Fact]
    public async Task Announce_DoubledModePriority_AttributesFirstDoubledAnnouncer()
    {
        // User (Bottom, Team1) announces Hearts; opponent overbids with NoTrumps, then Hearts
        // gets doubled — final mode resolves back to Hearts, announced by the user.
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        var deal = AddDeal(m, 1, GameMode.ColourHearts, Team.Team1, announcerWon: true, t1MatchPoints: 4, t2MatchPoints: 0);
        AddAction(deal, 1, ActionType.Announce, PlayerPosition.Bottom, GameMode.ColourHearts);
        AddAction(deal, 2, ActionType.Announce, PlayerPosition.Left, GameMode.NoTrumps);
        AddAction(deal, 3, ActionType.Double, PlayerPosition.Left, GameMode.ColourHearts);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(1, result.Bidding.DealsAnnounced);
        Assert.Equal(1, result.Bidding.AnnounceWins);
        var hearts = result.ModeStats.Single(s => s.Mode == GameMode.ColourHearts);
        Assert.Equal(1, hearts.TimesAnnounced);
        Assert.Equal(1, hearts.AnnounceWins);
        // The opponent's NoTrumps announce must not be attributed to anyone here.
        Assert.Equal(0, result.ModeStats.Single(s => s.Mode == GameMode.NoTrumps).TimesAnnounced);
    }

    [Fact]
    public async Task Announce_ByPartner_NotCountedForUser()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        var deal = AddDeal(m, 1, GameMode.ColourSpades, Team.Team1, announcerWon: true, t1MatchPoints: 2, t2MatchPoints: 0);
        // Partner (Top, also Team1) announced the final mode.
        AddAction(deal, 1, ActionType.Announce, PlayerPosition.Top, GameMode.ColourSpades);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(0, result.Bidding.DealsAnnounced);
        Assert.Equal(0, result.ModeStats.Single(s => s.Mode == GameMode.ColourSpades).TimesAnnounced);
    }

    [Fact]
    public async Task Announce_SameModeByOpponent_NotCountedForUser()
    {
        // The user played the final mode's announce action position-wise, but the deal was
        // won by the other team's announcement — the AnnouncerTeam guard must exclude it.
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: false);
        var deal = AddDeal(m, 1, GameMode.NoTrumps, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 2);
        AddAction(deal, 1, ActionType.Announce, PlayerPosition.Bottom, GameMode.NoTrumps);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(0, result.Bidding.DealsAnnounced);
    }

    #endregion

    #region Doubles and redoubles

    [Fact]
    public async Task Doubles_WonWhenAnnouncerLoses_AndOnlyOnFinalMode()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);

        // Deal 1: user doubles the final mode, announcer (Team2) loses -> double won
        var d1 = AddDeal(m, 1, GameMode.AllTrumps, Team.Team2, announcerWon: false, t1MatchPoints: 6, t2MatchPoints: 0);
        AddAction(d1, 1, ActionType.Double, PlayerPosition.Bottom, GameMode.AllTrumps);

        // Deal 2: user doubles the final mode, announcer wins -> double lost
        var d2 = AddDeal(m, 2, GameMode.NoTrumps, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 4);
        AddAction(d2, 1, ActionType.Double, PlayerPosition.Bottom, GameMode.NoTrumps);

        // Deal 3: user doubled a mode that was NOT the final one -> not counted
        var d3 = AddDeal(m, 3, GameMode.ColourClubs, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 2);
        AddAction(d3, 1, ActionType.Double, PlayerPosition.Bottom, GameMode.ColourDiamonds);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(2, result.Bidding.DoublesMade);
        Assert.Equal(1, result.Bidding.DoublesWon);
    }

    [Fact]
    public async Task Redoubles_WonWhenAnnouncerWins()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);

        var d1 = AddDeal(m, 1, GameMode.ColourHearts, Team.Team1, announcerWon: true, t1MatchPoints: 8, t2MatchPoints: 0);
        AddAction(d1, 1, ActionType.Redouble, PlayerPosition.Bottom, GameMode.ColourHearts);

        var d2 = AddDeal(m, 2, GameMode.ColourHearts, Team.Team1, announcerWon: false, t1MatchPoints: 0, t2MatchPoints: 8);
        AddAction(d2, 1, ActionType.Redouble, PlayerPosition.Bottom, GameMode.ColourHearts);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(2, result.Bidding.RedoublesMade);
        Assert.Equal(1, result.Bidding.RedoublesWon);
    }

    #endregion

    #region Sweeps and instant wins

    [Fact]
    public async Task Sweeps_AndInstantWins_SplitForAndAgainst()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);

        AddDeal(m, 1, GameMode.ColourClubs, Team.Team1, announcerWon: true, t1MatchPoints: 4, t2MatchPoints: 0,
            wasSweep: true, sweepingTeam: Team.Team1);
        AddDeal(m, 2, GameMode.ColourClubs, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 4,
            wasSweep: true, sweepingTeam: Team.Team2);
        AddDeal(m, 3, GameMode.NoTrumps, Team.Team1, announcerWon: true, t1MatchPoints: 6, t2MatchPoints: 0,
            isInstantWin: true);
        AddDeal(m, 4, GameMode.NoTrumps, Team.Team2, announcerWon: true, t1MatchPoints: 0, t2MatchPoints: 6,
            isInstantWin: true);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(1, result.Sweeps.SweepsFor);
        Assert.Equal(1, result.Sweeps.SweepsAgainst);
        Assert.Equal(1, result.Sweeps.InstantWinsFor);
        Assert.Equal(1, result.Sweeps.InstantWinsAgainst);
    }

    #endregion

    #region Recent form and activity

    [Fact]
    public async Task RecentForm_OldestFirst_CappedAtTen()
    {
        var (userId, player) = AddHuman("Alice");
        for (var i = 0; i < 12; i++)
        {
            var m = AddMatch(DaysAgo(12 - i));
            // Wins only the last (most recent) match
            AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: i == 11);
        }
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(10, result.Hero.RecentForm.Count);
        Assert.True(result.Hero.RecentForm[^1]);
        Assert.All(result.Hero.RecentForm.Take(9), w => Assert.False(w));
    }

    [Fact]
    public async Task Activity_GroupsMatchesByUtcDay()
    {
        var (userId, player) = AddHuman("Alice");
        var day = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        foreach (var at in new[] { day.AddHours(9), day.AddHours(21), day.AddDays(1) })
        {
            var m = AddMatch(at);
            AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        }
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(2, result.Activity.Count);
        Assert.Equal(new DateOnly(2026, 8, 10), result.Activity[0].Date);
        Assert.Equal(2, result.Activity[0].Count);
        Assert.Equal(new DateOnly(2026, 8, 11), result.Activity[1].Date);
        Assert.Equal(1, result.Activity[1].Count);
    }

    [Fact]
    public async Task AbandonedMatch_IsIncluded()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1), wasAbandoned: true);
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        AddDeal(m, 1, GameMode.ColourDiamonds, Team.Team1, announcerWon: true, t1MatchPoints: 2, t2MatchPoints: 0);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Single(result.Hero.RecentForm);
        Assert.Equal(1, result.ModeStats.Single(s => s.Mode == GameMode.ColourDiamonds).DealsPlayed);
    }

    #endregion

    #region Elo trend

    [Fact]
    public async Task EloTrend_ChronologicalOrder()
    {
        var (userId, player) = AddHuman("Alice");
        AddElo(player, DaysAgo(1), eloAfter: 1040);
        AddElo(player, DaysAgo(3), eloAfter: 1000);
        AddElo(player, DaysAgo(2), eloAfter: 1020);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(new[] { 1000, 1020, 1040 }, result.EloTrend.Select(p => p.Elo));
    }

    #endregion

    #region Partner and nemesis

    [Fact]
    public async Task PartnerAndNemesis_RespectThresholdAndPickExtremes()
    {
        var (userId, me) = AddHuman("Me");
        var (_, goodPartner) = AddHuman("GoodPartner");
        var (_, mehPartner) = AddHuman("MehPartner");
        var (_, nemesis) = AddHuman("Nemesis");
        var (_, rare) = AddHuman("Rare");

        // 5 games with GoodPartner (4 wins), nemesis opposing (I win 1 of 5)
        for (var i = 0; i < 5; i++)
        {
            var m = AddMatch(DaysAgo(20 - i));
            var iWin = i < 4;
            AddMatchPlayer(m, me, PlayerPosition.Bottom, Team.Team1, isWinner: iWin);
            AddMatchPlayer(m, goodPartner, PlayerPosition.Top, Team.Team1, isWinner: iWin);
            AddMatchPlayer(m, nemesis, PlayerPosition.Left, Team.Team2, isWinner: !iWin);
        }
        // Re-pair: 5 games with MehPartner (1 win), nemesis again opposing (I win 0 of 5)
        for (var i = 0; i < 5; i++)
        {
            var m = AddMatch(DaysAgo(10 - i));
            var iWin = i == 0;
            AddMatchPlayer(m, me, PlayerPosition.Bottom, Team.Team1, isWinner: iWin);
            AddMatchPlayer(m, mehPartner, PlayerPosition.Top, Team.Team1, isWinner: iWin);
            AddMatchPlayer(m, nemesis, PlayerPosition.Left, Team.Team2, isWinner: !iWin);
        }
        // Below threshold: 1 game with Rare
        var single = AddMatch(DaysAgo(1));
        AddMatchPlayer(single, me, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        AddMatchPlayer(single, rare, PlayerPosition.Top, Team.Team1, isWinner: true);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.NotNull(result.BestPartner);
        Assert.Equal("GoodPartner", result.BestPartner!.DisplayName);
        Assert.Equal(5, result.BestPartner.Games);
        Assert.Equal(4, result.BestPartner.Wins);
        Assert.Equal(80, result.BestPartner.WinRate);

        Assert.NotNull(result.Nemesis);
        Assert.Equal("Nemesis", result.Nemesis!.DisplayName);
        Assert.Equal(10, result.Nemesis.Games);
        Assert.Equal(5, result.Nemesis.Wins); // 4 wins in the first block + 1 in the second
    }

    [Fact]
    public async Task PartnerAndNemesis_BelowThreshold_AreNull()
    {
        var (userId, me) = AddHuman("Me");
        var (_, partner) = AddHuman("Partner");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, me, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        AddMatchPlayer(m, partner, PlayerPosition.Top, Team.Team1, isWinner: true);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Null(result.BestPartner);
        Assert.Null(result.Nemesis);
    }

    #endregion

    #region Callouts

    [Fact]
    public async Task Callouts_OnFire_WhenStreakAtLeastThree()
    {
        var (userId, _) = AddHuman("Alice", winStreak: 4);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        var onFire = Assert.Single(result.Callouts, c => c.Code == "onFire");
        Assert.Equal("strength", onFire.Kind);
        Assert.Equal(4, onFire.Value);
    }

    [Fact]
    public async Task Callouts_BestAndWorstMode_RequireTenDeals()
    {
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);

        short dealNo = 1;
        // 10 Hearts deals, 8 won -> bestMode strength
        for (var i = 0; i < 10; i++)
        {
            var won = i < 8;
            AddDeal(m, dealNo++, GameMode.ColourHearts, Team.Team1, announcerWon: won,
                t1MatchPoints: won ? 2 : 0, t2MatchPoints: won ? 0 : 2);
        }
        // 10 NoTrumps deals, 2 won -> worstMode weakness
        for (var i = 0; i < 10; i++)
        {
            var won = i < 2;
            AddDeal(m, dealNo++, GameMode.NoTrumps, Team.Team1, announcerWon: won,
                t1MatchPoints: won ? 3 : 0, t2MatchPoints: won ? 0 : 3);
        }
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        var best = Assert.Single(result.Callouts, c => c.Code == "bestMode");
        Assert.Equal(GameMode.ColourHearts, best.Mode);
        Assert.Equal(80, best.Value);
        var worst = Assert.Single(result.Callouts, c => c.Code == "worstMode");
        Assert.Equal(GameMode.NoTrumps, worst.Mode);
    }

    #endregion

    #region Tricks

    [Fact]
    public async Task Tricks_ReplayCountsPersonalWins()
    {
        // User sits Bottom (Team1); mode ColourHearts, so hearts are trump.
        var (userId, player) = AddHuman("Alice");
        var m = AddMatch(DaysAgo(1));
        AddMatchPlayer(m, player, PlayerPosition.Bottom, Team.Team1, isWinner: true);
        var deal = AddDeal(m, 1, GameMode.ColourHearts, Team.Team1, announcerWon: true, t1MatchPoints: 2, t2MatchPoints: 0);

        // Trick 1: user leads Ace of Spades but Left trumps with a heart -> user loses.
        AddPlay(deal, 1, PlayerPosition.Bottom, CardRank.Ace, CardSuit.Spades, trick: 1);
        AddPlay(deal, 2, PlayerPosition.Left, CardRank.Seven, CardSuit.Hearts, trick: 1);
        AddPlay(deal, 3, PlayerPosition.Top, CardRank.Ten, CardSuit.Spades, trick: 1);
        AddPlay(deal, 4, PlayerPosition.Right, CardRank.Eight, CardSuit.Spades, trick: 1);

        // Trick 2: user leads Ace of Diamonds, nobody trumps -> user wins.
        AddPlay(deal, 5, PlayerPosition.Bottom, CardRank.Ace, CardSuit.Diamonds, trick: 2);
        AddPlay(deal, 6, PlayerPosition.Left, CardRank.King, CardSuit.Diamonds, trick: 2);
        AddPlay(deal, 7, PlayerPosition.Top, CardRank.Seven, CardSuit.Diamonds, trick: 2);
        AddPlay(deal, 8, PlayerPosition.Right, CardRank.Eight, CardSuit.Diamonds, trick: 2);

        // Trick 5 is incomplete (3 cards) and must be ignored.
        AddPlay(deal, 9, PlayerPosition.Left, CardRank.Nine, CardSuit.Clubs, trick: 5);
        AddPlay(deal, 10, PlayerPosition.Top, CardRank.Ten, CardSuit.Clubs, trick: 5);
        AddPlay(deal, 11, PlayerPosition.Right, CardRank.Queen, CardSuit.Clubs, trick: 5);

        // Trick 8 (last): user trumps the club lead with the trump Jack -> user wins the last trick.
        AddPlay(deal, 12, PlayerPosition.Left, CardRank.Ten, CardSuit.Clubs, trick: 8);
        AddPlay(deal, 13, PlayerPosition.Bottom, CardRank.Jack, CardSuit.Hearts, trick: 8);
        AddPlay(deal, 14, PlayerPosition.Top, CardRank.Nine, CardSuit.Clubs, trick: 8);
        AddPlay(deal, 15, PlayerPosition.Right, CardRank.Seven, CardSuit.Clubs, trick: 8);

        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal(1, result.Tricks.AnalyzedDeals);
        Assert.Equal(3, result.Tricks.TricksPlayed);
        Assert.Equal(2, result.Tricks.TricksWon);
        Assert.Equal(66.7, result.Tricks.TrickWinRate);
        Assert.Equal(1, result.Tricks.LastTrickWins);
        Assert.Equal(2, result.Tricks.BestTricksInOneDeal);
    }

    #endregion

    #region Public player view

    [Fact]
    public async Task PublicHighlights_UnknownPlayer_ReturnsNull()
    {
        var result = await _service.GetPlayerHighlightsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task PublicHighlights_PrivateElo_HidesEloAndTrend()
    {
        var (_, player) = AddHuman("Alice", elo: 1500); // EloIsPublic defaults to false
        AddElo(player, DaysAgo(1), eloAfter: 1500);
        await _db.SaveChangesAsync();

        var result = await _service.GetPlayerHighlightsAsync(player.Id);

        Assert.NotNull(result);
        Assert.Equal("Alice", result!.PlayerName);
        Assert.Null(result.Hero.EloRating);
        Assert.Empty(result.EloTrend);
    }

    [Fact]
    public async Task PublicHighlights_PublicElo_ShowsEloAndTrend()
    {
        var (_, player) = AddHuman("Alice", elo: 1500, eloIsPublic: true);
        AddElo(player, DaysAgo(1), eloAfter: 1500);
        await _db.SaveChangesAsync();

        var result = await _service.GetPlayerHighlightsAsync(player.Id);

        Assert.NotNull(result);
        Assert.Equal(1500, result!.Hero.EloRating);
        Assert.Single(result.EloTrend);
    }

    [Fact]
    public async Task MyHighlights_ShowEloEvenWhenPrivate_AndCarryMyName()
    {
        var (userId, player) = AddHuman("Alice", elo: 1500); // private Elo
        AddElo(player, DaysAgo(1), eloAfter: 1500);
        await _db.SaveChangesAsync();

        var result = await _service.GetHighlightsAsync(userId);

        Assert.Equal("Alice", result.PlayerName);
        Assert.Equal(1500, result.Hero.EloRating);
        Assert.Single(result.EloTrend);
    }

    #endregion

    #region Helpers

    private static DateTimeOffset DaysAgo(int days) => DateTimeOffset.UtcNow.AddDays(-days);

    private (Guid UserId, Player Player) AddHuman(
        string displayName,
        int elo = 1000,
        int gamesPlayed = 10,
        int gamesWon = 5,
        int winStreak = 0,
        bool eloIsPublic = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.NewGuid(),
            Username = displayName.ToLowerInvariant(),
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);

        var player = new Player
        {
            Id = Guid.NewGuid(),
            PlayerType = PlayerType.Human,
            UserId = user.Id,
            User = user,
            EloRating = elo,
            EloIsPublic = eloIsPublic,
            GamesPlayed = gamesPlayed,
            GamesWon = gamesWon,
            WinStreak = winStreak,
        };
        _db.Players.Add(player);

        return (user.Id, player);
    }

    private Match AddMatch(DateTimeOffset completedAt, bool wasAbandoned = false)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoomName = "test-room",
            TargetScore = 150,
            WasAbandoned = wasAbandoned,
            StartedAt = completedAt.AddMinutes(-30),
            CompletedAt = completedAt,
            CreatedAt = completedAt,
        };
        _db.Matches.Add(match);
        return match;
    }

    private void AddMatchPlayer(Match match, Player player, PlayerPosition position, Team team, bool isWinner)
    {
        _db.MatchPlayers.Add(new MatchPlayer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            Match = match,
            PlayerId = player.Id,
            Player = player,
            Position = position,
            Team = team,
            IsWinner = isWinner,
        });
    }

    private Deal AddDeal(
        Match match,
        short dealNumber,
        GameMode mode,
        Team announcerTeam,
        bool announcerWon,
        int t1MatchPoints,
        int t2MatchPoints,
        int? t1CardPoints = null,
        int? t2CardPoints = null,
        bool wasSweep = false,
        Team? sweepingTeam = null,
        bool isInstantWin = false)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            Match = match,
            DealNumber = dealNumber,
            DealerPosition = PlayerPosition.Bottom,
            GameMode = mode,
            AnnouncerTeam = announcerTeam,
            Multiplier = MultiplierState.Normal,
            Team1MatchPoints = t1MatchPoints,
            Team2MatchPoints = t2MatchPoints,
            Team1CardPoints = t1CardPoints,
            Team2CardPoints = t2CardPoints,
            WasSweep = wasSweep,
            SweepingTeam = sweepingTeam,
            IsInstantWin = isInstantWin,
            AnnouncerWon = announcerWon,
            StartedAt = match.StartedAt,
            CompletedAt = match.CompletedAt,
        };
        _db.Deals.Add(deal);
        return deal;
    }

    private void AddAction(Deal deal, short order, ActionType type, PlayerPosition position, GameMode mode)
    {
        _db.DealActions.Add(new DealAction
        {
            Id = Guid.NewGuid(),
            DealId = deal.Id,
            Deal = deal,
            ActionOrder = order,
            ActionType = type,
            PlayerPosition = position,
            GameMode = mode,
        });
    }

    private void AddPlay(Deal deal, short order, PlayerPosition position, CardRank rank, CardSuit suit, short trick)
    {
        _db.DealActions.Add(new DealAction
        {
            Id = Guid.NewGuid(),
            DealId = deal.Id,
            Deal = deal,
            ActionOrder = order,
            ActionType = ActionType.PlayCard,
            PlayerPosition = position,
            CardRank = rank,
            CardSuit = suit,
            TrickNumber = trick,
        });
    }

    private void AddElo(Player player, DateTimeOffset recordedAt, int eloAfter)
    {
        _db.EloHistories.Add(new EloHistory
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Player = player,
            MatchId = Guid.NewGuid(),
            EloBefore = eloAfter - 20,
            EloAfter = eloAfter,
            EloChange = 20,
            RecordedAt = recordedAt,
        });
    }

    #endregion
}
