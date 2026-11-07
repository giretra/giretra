using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CoreGameMode = Giretra.Core.GameModes.GameMode;
using CoreMultiplier = Giretra.Core.GameModes.MultiplierState;
using CorePosition = Giretra.Core.Players.PlayerPosition;
using CoreRank = Giretra.Core.Cards.CardRank;
using CoreSuit = Giretra.Core.Cards.CardSuit;
using CoreTeam = Giretra.Core.Players.Team;

namespace Giretra.Web.Tests.Services;

public sealed class MatchSummaryServiceTests : IDisposable
{
    private static readonly CorePosition B = CorePosition.Bottom;
    private static readonly CorePosition L = CorePosition.Left;
    private static readonly CorePosition T = CorePosition.Top;
    private static readonly CorePosition R = CorePosition.Right;

    /// <summary>Same oracle deal as the replay tests: All Trumps, winners B L B T B R B L.</summary>
    private static readonly (CorePosition Player, CoreRank Rank, CoreSuit Suit)[] OraclePlays =
    [
        (B, CoreRank.Jack, CoreSuit.Spades), (L, CoreRank.Nine, CoreSuit.Spades), (T, CoreRank.Ace, CoreSuit.Spades), (R, CoreRank.Ten, CoreSuit.Spades),
        (B, CoreRank.Seven, CoreSuit.Spades), (L, CoreRank.King, CoreSuit.Spades), (T, CoreRank.Queen, CoreSuit.Spades), (R, CoreRank.Eight, CoreSuit.Spades),
        (L, CoreRank.Nine, CoreSuit.Hearts), (T, CoreRank.Ace, CoreSuit.Hearts), (R, CoreRank.Ten, CoreSuit.Hearts), (B, CoreRank.Jack, CoreSuit.Hearts),
        (B, CoreRank.Eight, CoreSuit.Hearts), (L, CoreRank.Seven, CoreSuit.Hearts), (T, CoreRank.King, CoreSuit.Hearts), (R, CoreRank.Queen, CoreSuit.Hearts),
        (T, CoreRank.Ace, CoreSuit.Diamonds), (R, CoreRank.Ten, CoreSuit.Diamonds), (B, CoreRank.Jack, CoreSuit.Diamonds), (L, CoreRank.Nine, CoreSuit.Diamonds),
        (B, CoreRank.Queen, CoreSuit.Diamonds), (L, CoreRank.Eight, CoreSuit.Diamonds), (T, CoreRank.Seven, CoreSuit.Diamonds), (R, CoreRank.King, CoreSuit.Diamonds),
        (R, CoreRank.Ten, CoreSuit.Clubs), (B, CoreRank.Jack, CoreSuit.Clubs), (L, CoreRank.Nine, CoreSuit.Clubs), (T, CoreRank.Ace, CoreSuit.Clubs),
        (B, CoreRank.Seven, CoreSuit.Clubs), (L, CoreRank.King, CoreSuit.Clubs), (T, CoreRank.Queen, CoreSuit.Clubs), (R, CoreRank.Eight, CoreSuit.Clubs),
    ];

    private static readonly CorePosition[] OracleWinners = [B, L, B, T, B, R, B, L];

    private readonly GiretraDbContext _db;
    private readonly MatchSummaryService _service;

    public MatchSummaryServiceTests()
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GiretraDbContext(options);
        _service = new MatchSummaryService(_db, NullLogger<MatchSummaryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    #region Stats

    [Fact]
    public void Stats_LeadChangesSwingsRunsAndComeback_FromHandWrittenScoreline()
    {
        // Team1 scores 2, then Team2 scores 3 and 3 (lead change at deal 2), Team1 scores 7 (lead change
        // back at deal 4), Team2 scores 1 (no change: 9 vs 7), Team1 scores 2 (tie, leader kept), Team1 scores 2.
        var deals = Scoreline((2, 0), (0, 3), (0, 3), (7, 0), (0, 1), (2, 0), (2, 0));

        var stats = MatchSummaryStats.Compute(deals, CoreTeam.Team1);

        Assert.Equal(2, stats.LeadChanges);
        Assert.Equal(4, stats.BiggestSwingDeal);
        Assert.Equal(7, stats.BiggestSwingPoints);
        Assert.Equal(CoreTeam.Team1, stats.BiggestSwingTeam);
        // Team1 scores deals 6 and 7 back to back, Team2 scores deals 2 and 3: both runs are 2, first found wins.
        Assert.Equal(2, stats.LongestRunDeals);
        Assert.Equal(CoreTeam.Team2, stats.LongestRunTeam);
        // Team1 trailed 2–6 after deal 3.
        Assert.Equal(4, stats.ComebackFrom);
    }

    [Fact]
    public void Stats_WinnerNeverTrailing_HasNoComeback_AndTiesDoNotCountAsLeadChanges()
    {
        var deals = Scoreline((2, 0), (0, 2), (3, 0));

        var stats = MatchSummaryStats.Compute(deals, CoreTeam.Team1);

        Assert.Equal(0, stats.ComebackFrom);
        Assert.Equal(0, stats.LeadChanges);
    }

    [Fact]
    public void Stats_ContractsSweepsDoublesAndModes_AreCountedPerTeam()
    {
        var deals = new List<MatchSummaryDealResponse>
        {
            Deal(1, CoreTeam.Team1, CoreGameMode.AllTrumps, announcerWon: true, multiplier: CoreMultiplier.Doubled),
            Deal(2, CoreTeam.Team1, CoreGameMode.ColourHearts, announcerWon: false),
            Deal(3, CoreTeam.Team2, CoreGameMode.ColourHearts, announcerWon: true, sweep: CoreTeam.Team2),
            Deal(4, CoreTeam.Team2, CoreGameMode.NoTrumps, announcerWon: true, multiplier: CoreMultiplier.Redoubled),
        };

        var stats = MatchSummaryStats.Compute(deals, CoreTeam.Team2);

        Assert.Equal(2, stats.Team1Contracts);
        Assert.Equal(1, stats.Team1ContractsMade);
        Assert.Equal(2, stats.Team2Contracts);
        Assert.Equal(2, stats.Team2ContractsMade);
        Assert.Equal(0, stats.Team1Sweeps);
        Assert.Equal(1, stats.Team2Sweeps);
        Assert.Equal(2, stats.DoubledDeals);
        Assert.Equal(2, stats.DealsByMode[CoreGameMode.ColourHearts]);
        Assert.Equal(1, stats.DealsByMode[CoreGameMode.AllTrumps]);
        Assert.Equal(1, stats.DealsByMode[CoreGameMode.NoTrumps]);
    }

    [Fact]
    public void FinalAnnouncer_IsThePlayerOfTheLastAnnounce()
    {
        var negotiation = new List<NegotiationActionResponse>
        {
            new() { ActionType = "Announce", Player = L, Mode = CoreGameMode.ColourClubs },
            new() { ActionType = "Announce", Player = T, Mode = CoreGameMode.NoTrumps },
            new() { ActionType = "Double", Player = R, Mode = CoreGameMode.NoTrumps },
            new() { ActionType = "Accept", Player = B, Mode = CoreGameMode.NoTrumps },
        };

        Assert.Equal(T, MatchSummaryStats.FinalAnnouncer(negotiation));
        Assert.Null(MatchSummaryStats.FinalAnnouncer([]));
    }

    #endregion

    #region Service

    [Fact]
    public async Task Participant_GetsDealsWithRunningScoreNegotiationAndTrickWinners()
    {
        var (userId, alice) = AddHuman("Alice");
        var match = AddMatch(completed: true, winner: Team.Team1, t1: 4, t2: 3);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        AddSeat(match, AddHuman("Bob").Player, PlayerPosition.Top, Team.Team1);
        AddSeat(match, AddBot("Rakoto"), PlayerPosition.Left, Team.Team2);
        AddSeat(match, AddBot("Rasoa"), PlayerPosition.Right, Team.Team2);
        AddDeal(match, 1, t1: 2, t2: 0, withPlays: true, announcer: PlayerPosition.Top);
        AddDeal(match, 2, t1: 0, t2: 3, withPlays: false, announcer: PlayerPosition.Left, announcerTeam: Team.Team2);
        AddDeal(match, 3, t1: 2, t2: 0, withPlays: true, announcer: PlayerPosition.Bottom, multiplier: MultiplierState.Doubled);
        await _db.SaveChangesAsync();

        var result = await _service.GetSummaryAsync(match.Id, userId);

        Assert.Equal(MatchSummaryStatus.Ok, result.Status);
        var summary = result.Summary!;
        Assert.Equal(CoreTeam.Team1, summary.WinnerTeam);
        Assert.Equal(B, summary.ViewerPosition);
        Assert.Equal(3, summary.Deals.Count);

        var first = summary.Deals[0];
        Assert.Equal(2, first.Team1ScoreAfter);
        Assert.Equal(0, first.Team2ScoreAfter);
        Assert.Equal(T, first.Announcer);
        Assert.Equal(OracleWinners, first.TrickWinners.ToArray());
        Assert.Equal(5, first.Team1Tricks);
        Assert.Equal(3, first.Team2Tricks);
        Assert.True(first.IsReplayable);
        Assert.Equal(["Announce", "Accept", "Accept", "Accept"], first.Negotiation.Select(n => n.ActionType).ToArray());

        var second = summary.Deals[1];
        Assert.Equal(2, second.Team1ScoreAfter);
        Assert.Equal(3, second.Team2ScoreAfter);
        Assert.Empty(second.TrickWinners);
        Assert.False(second.IsReplayable);

        var third = summary.Deals[2];
        Assert.Equal(4, third.Team1ScoreAfter);
        Assert.Equal(3, third.Team2ScoreAfter);
        Assert.Equal(CoreMultiplier.Doubled, third.Multiplier);

        var bottom = summary.Players.Single(p => p.Position == B);
        Assert.Equal("Alice", bottom.DisplayName);
        Assert.Equal(8, bottom.TricksWon); // 4 per oracle deal × 2 replayable deals
        Assert.Equal(1, bottom.Contracts);
        Assert.Equal(1, bottom.ContractsMade);
        var top = summary.Players.Single(p => p.Position == T);
        Assert.Equal(2, top.TricksWon);
        Assert.Equal(1, top.Contracts);

        Assert.Equal(2, summary.Stats.LeadChanges);
        Assert.Equal(1, summary.Stats.DoubledDeals);
        Assert.Equal(10, summary.Stats.Team1Tricks);
        Assert.Equal(6, summary.Stats.Team2Tricks);
    }

    [Fact]
    public async Task Stranger_IsForbidden_AndUnknownMatchIsNotFound()
    {
        var (_, alice) = AddHuman("Alice");
        var (strangerId, _) = AddHuman("Mallory");
        var match = AddMatch(completed: true, winner: Team.Team1, t1: 2, t2: 0);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        await _db.SaveChangesAsync();

        Assert.Equal(MatchSummaryStatus.Forbidden, (await _service.GetSummaryAsync(match.Id, strangerId)).Status);
        Assert.Equal(MatchSummaryStatus.MatchNotFound, (await _service.GetSummaryAsync(Guid.NewGuid(), strangerId)).Status);
    }

    [Fact]
    public async Task AbandonedMatch_IsSummarised_ButNotReplayable()
    {
        var (userId, alice) = AddHuman("Alice");
        var match = AddMatch(completed: true, winner: Team.Team2, t1: 2, t2: 0, abandoned: true);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        AddDeal(match, 1, t1: 2, t2: 0, withPlays: true, announcer: PlayerPosition.Bottom);
        await _db.SaveChangesAsync();

        var result = await _service.GetSummaryAsync(match.Id, userId);

        Assert.Equal(MatchSummaryStatus.Ok, result.Status);
        Assert.True(result.Summary!.WasAbandoned);
        Assert.False(result.Summary.Deals[0].IsReplayable);
        Assert.Equal(OracleWinners, result.Summary.Deals[0].TrickWinners.ToArray());
    }

    [Fact]
    public async Task RunningMatch_IsNotCompleted()
    {
        var (userId, alice) = AddHuman("Alice");
        var match = AddMatch(completed: false, winner: null, t1: 0, t2: 0);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        await _db.SaveChangesAsync();

        Assert.Equal(MatchSummaryStatus.NotCompleted, (await _service.GetSummaryAsync(match.Id, userId)).Status);
    }

    #endregion

    #region Builders

    private static List<MatchSummaryDealResponse> Scoreline(params (int T1, int T2)[] points)
    {
        var deals = new List<MatchSummaryDealResponse>();
        var t1 = 0;
        var t2 = 0;
        for (var i = 0; i < points.Length; i++)
        {
            t1 += points[i].T1;
            t2 += points[i].T2;
            deals.Add(Deal(i + 1, CoreTeam.Team1, CoreGameMode.AllTrumps, announcerWon: true,
                points: (points[i].T1, points[i].T2, t1, t2)));
        }
        return deals;
    }

    private static MatchSummaryDealResponse Deal(int number, CoreTeam announcerTeam, CoreGameMode mode, bool announcerWon,
        CoreMultiplier multiplier = CoreMultiplier.Normal, CoreTeam? sweep = null,
        (int T1, int T2, int After1, int After2) points = default) => new()
    {
        DealNumber = number,
        Dealer = R,
        GameMode = mode,
        Multiplier = multiplier,
        AnnouncerTeam = announcerTeam,
        Announcer = announcerTeam == CoreTeam.Team1 ? B : L,
        AnnouncerWon = announcerWon,
        Team1MatchPoints = points.T1,
        Team2MatchPoints = points.T2,
        Team1ScoreAfter = points.After1,
        Team2ScoreAfter = points.After2,
        WasSweep = sweep != null,
        SweepingTeam = sweep,
        IsInstantWin = false,
        Negotiation = [],
        TrickWinners = [],
        Team1Tricks = 0,
        Team2Tricks = 0,
        IsReplayable = false,
    };

    private (Guid UserId, Player Player) AddHuman(string name)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.NewGuid(),
            Username = name.ToLowerInvariant(),
            DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var player = new Player
        {
            Id = Guid.NewGuid(),
            PlayerType = PlayerType.Human,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        _db.Players.Add(player);
        return (user.Id, player);
    }

    private Player AddBot(string name)
    {
        var bot = new Bot
        {
            Id = Guid.NewGuid(),
            AgentType = "Test",
            AgentTypeFactory = "Test",
            DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var player = new Player
        {
            Id = Guid.NewGuid(),
            PlayerType = PlayerType.Bot,
            BotId = bot.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Bots.Add(bot);
        _db.Players.Add(player);
        return player;
    }

    private Match AddMatch(bool completed, Team? winner, int t1, int t2, bool abandoned = false)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoomName = "Room",
            TargetScore = 150,
            TotalDeals = 3,
            IsRanked = true,
            WasAbandoned = abandoned,
            WinnerTeam = winner,
            Team1FinalScore = t1,
            Team2FinalScore = t2,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt = completed ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        _db.Matches.Add(match);
        return match;
    }

    private void AddSeat(Match match, Player player, PlayerPosition position, Team team)
    {
        _db.MatchPlayers.Add(new MatchPlayer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            PlayerId = player.Id,
            Position = position,
            Team = team,
        });
    }

    private void AddDeal(Match match, int number, int t1, int t2, bool withPlays, PlayerPosition announcer,
        Team announcerTeam = Team.Team1, MultiplierState multiplier = MultiplierState.Normal)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DealNumber = (short)number,
            DealerPosition = PlayerPosition.Right,
            GameMode = GameMode.AllTrumps,
            AnnouncerTeam = announcerTeam,
            Multiplier = multiplier,
            Team1CardPoints = 227,
            Team2CardPoints = 31,
            Team1MatchPoints = t1,
            Team2MatchPoints = t2,
            AnnouncerWon = (announcerTeam == Team.Team1) == (t1 > t2),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow,
        };

        short order = 0;
        deal.DealActions.Add(new DealAction
        {
            Id = Guid.NewGuid(), DealId = deal.Id, ActionOrder = order++, ActionType = ActionType.Announce,
            PlayerPosition = announcer, GameMode = GameMode.AllTrumps,
        });
        var next = announcer;
        for (var i = 0; i < 3; i++)
        {
            next = (PlayerPosition)(((int)next + 1) % 4);
            deal.DealActions.Add(new DealAction
            {
                Id = Guid.NewGuid(), DealId = deal.Id, ActionOrder = order++, ActionType = ActionType.Accept,
                PlayerPosition = next, GameMode = GameMode.AllTrumps,
            });
        }

        if (withPlays)
        {
            for (var i = 0; i < OraclePlays.Length; i++)
            {
                var (player, rank, suit) = OraclePlays[i];
                deal.DealActions.Add(new DealAction
                {
                    Id = Guid.NewGuid(), DealId = deal.Id, ActionOrder = order++, ActionType = ActionType.PlayCard,
                    PlayerPosition = (PlayerPosition)(int)player, CardRank = (CardRank)(int)rank,
                    CardSuit = (CardSuit)(int)suit, TrickNumber = (short)(i / 4 + 1),
                });
            }
        }

        _db.Deals.Add(deal);
    }

    #endregion
}
