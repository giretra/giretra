using Giretra.Core.Play;
using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Services.Replay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CoreCard = Giretra.Core.Cards.Card;
using CoreGameMode = Giretra.Core.GameModes.GameMode;
using CorePosition = Giretra.Core.Players.PlayerPosition;
using CoreRank = Giretra.Core.Cards.CardRank;
using CoreSuit = Giretra.Core.Cards.CardSuit;
using CoreTeam = Giretra.Core.Players.Team;

namespace Giretra.Web.Tests.Services;

/// <summary>
/// The oracle deal is All Trumps, built so every trick winner is obvious without the engine:
/// each trick is four cards of one suit, so the Jack (or, in the low trick, the King) wins.
/// Point values: J 20, 9 14, A 11, 10 10, K 4, Q 3, 8 0, 7 0; +10 for the last trick.
/// </summary>
public sealed class DealReplayServiceTests : IDisposable
{
    private static readonly CorePosition B = CorePosition.Bottom;
    private static readonly CorePosition L = CorePosition.Left;
    private static readonly CorePosition T = CorePosition.Top;
    private static readonly CorePosition R = CorePosition.Right;

    private static readonly (CorePosition Player, CoreRank Rank, CoreSuit Suit)[] OraclePlays =
    [
        // Trick 1, spades high, leader Bottom → Bottom (J)        55 pts
        (B, CoreRank.Jack, CoreSuit.Spades), (L, CoreRank.Nine, CoreSuit.Spades), (T, CoreRank.Ace, CoreSuit.Spades), (R, CoreRank.Ten, CoreSuit.Spades),
        // Trick 2, spades low, leader Bottom → Left (K)            7 pts
        (B, CoreRank.Seven, CoreSuit.Spades), (L, CoreRank.King, CoreSuit.Spades), (T, CoreRank.Queen, CoreSuit.Spades), (R, CoreRank.Eight, CoreSuit.Spades),
        // Trick 3, hearts high, leader Left → Bottom (J)          55 pts
        (L, CoreRank.Nine, CoreSuit.Hearts), (T, CoreRank.Ace, CoreSuit.Hearts), (R, CoreRank.Ten, CoreSuit.Hearts), (B, CoreRank.Jack, CoreSuit.Hearts),
        // Trick 4, hearts low, leader Bottom → Top (K)             7 pts
        (B, CoreRank.Eight, CoreSuit.Hearts), (L, CoreRank.Seven, CoreSuit.Hearts), (T, CoreRank.King, CoreSuit.Hearts), (R, CoreRank.Queen, CoreSuit.Hearts),
        // Trick 5, diamonds high, leader Top → Bottom (J)         55 pts
        (T, CoreRank.Ace, CoreSuit.Diamonds), (R, CoreRank.Ten, CoreSuit.Diamonds), (B, CoreRank.Jack, CoreSuit.Diamonds), (L, CoreRank.Nine, CoreSuit.Diamonds),
        // Trick 6, diamonds low, leader Bottom → Right (K)         7 pts
        (B, CoreRank.Queen, CoreSuit.Diamonds), (L, CoreRank.Eight, CoreSuit.Diamonds), (T, CoreRank.Seven, CoreSuit.Diamonds), (R, CoreRank.King, CoreSuit.Diamonds),
        // Trick 7, clubs high, leader Right → Bottom (J)          55 pts
        (R, CoreRank.Ten, CoreSuit.Clubs), (B, CoreRank.Jack, CoreSuit.Clubs), (L, CoreRank.Nine, CoreSuit.Clubs), (T, CoreRank.Ace, CoreSuit.Clubs),
        // Trick 8, clubs low, leader Bottom → Left (K)             7 + 10 pts
        (B, CoreRank.Seven, CoreSuit.Clubs), (L, CoreRank.King, CoreSuit.Clubs), (T, CoreRank.Queen, CoreSuit.Clubs), (R, CoreRank.Eight, CoreSuit.Clubs),
    ];

    private static readonly CorePosition[] OracleWinners = [B, L, B, T, B, R, B, L];
    private static readonly int[] OracleTeam1Cumulative = [55, 55, 110, 117, 172, 172, 227, 227];
    private static readonly int[] OracleTeam2Cumulative = [0, 7, 7, 7, 7, 14, 14, 31];

    private readonly GiretraDbContext _db;
    private readonly DealReplayService _service;

    public DealReplayServiceTests()
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GiretraDbContext(options);
        _service = new DealReplayService(_db, NullLogger<DealReplayService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    #region Assembler against the oracle

    [Fact]
    public void Assembler_ReplaysOracleDeal_WinnersAndPointsMatch()
    {
        var plays = OraclePlays.Select(p => new PlayedCard(p.Player, new CoreCard(p.Rank, p.Suit))).ToList();

        var result = DealReplayAssembler.Assemble(CoreGameMode.AllTrumps, plays);

        Assert.Equal(8, result.Tricks.Count);
        for (var i = 0; i < 8; i++)
        {
            var trick = result.Tricks[i];
            Assert.Equal(i + 1, trick.TrickNumber);
            Assert.Equal(4, trick.Plays.Count);
            Assert.Equal(OracleWinners[i], trick.Winner);
            Assert.Equal(OracleTeam1Cumulative[i], trick.Team1CumulativeCardPoints);
            Assert.Equal(OracleTeam2Cumulative[i], trick.Team2CumulativeCardPoints);
            if (i > 0)
                Assert.Equal(OracleWinners[i - 1], trick.Leader);
        }

        Assert.Equal(227, result.Team1CardPoints);
        Assert.Equal(31, result.Team2CardPoints);
        Assert.Equal(258, result.Team1CardPoints + result.Team2CardPoints);
    }

    [Fact]
    public void Assembler_RebuildsStartingHands_EightCardsEachMatchingThePlays()
    {
        var plays = OraclePlays.Select(p => new PlayedCard(p.Player, new CoreCard(p.Rank, p.Suit))).ToList();

        var result = DealReplayAssembler.Assemble(CoreGameMode.AllTrumps, plays);

        Assert.Equal(4, result.Hands.Count);
        foreach (var hand in result.Hands)
        {
            Assert.Equal(8, hand.Cards.Count);
            var expected = plays.Where(p => p.Player == hand.Position).Select(p => (p.Card.Rank, p.Card.Suit)).ToHashSet();
            var actual = hand.Cards.Select(c => (c.Rank, c.Suit)).ToHashSet();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Assembler_RejectsIncompleteOrInconsistentPlays()
    {
        var plays = OraclePlays.Select(p => new PlayedCard(p.Player, new CoreCard(p.Rank, p.Suit))).ToList();

        Assert.Throws<InvalidDataException>(() => DealReplayAssembler.Assemble(CoreGameMode.AllTrumps, plays.Take(31).ToList()));

        var wrongOrder = plays.ToList();
        (wrongOrder[1], wrongOrder[2]) = (wrongOrder[2], wrongOrder[1]);
        Assert.Throws<InvalidDataException>(() => DealReplayAssembler.Assemble(CoreGameMode.AllTrumps, wrongOrder));

        var duplicate = plays.ToList();
        duplicate[5] = duplicate[5] with { Card = duplicate[0].Card };
        Assert.Throws<InvalidDataException>(() => DealReplayAssembler.Assemble(CoreGameMode.AllTrumps, duplicate));
    }

    #endregion

    #region Service

    [Fact]
    public async Task Participant_GetsFullReplay_WithContextAndScoresBefore()
    {
        var (userId, player) = AddHuman("Alice");
        var match = AddMatch(completed: true);
        AddSeat(match, player, PlayerPosition.Bottom, Team.Team1);
        AddSeat(match, AddHuman("Bob").Player, PlayerPosition.Top, Team.Team1);
        AddSeat(match, AddBot("Rakoto"), PlayerPosition.Left, Team.Team2);
        AddSeat(match, AddBot("Rasoa"), PlayerPosition.Right, Team.Team2);
        AddDeal(match, 1, t1MatchPoints: 2, t2MatchPoints: 0, withPlays: false);
        AddDeal(match, 2, t1MatchPoints: 0, t2MatchPoints: 3, withPlays: false);
        AddDeal(match, 3, t1MatchPoints: 2, t2MatchPoints: 0, withPlays: true, multiplier: MultiplierState.Doubled);
        await _db.SaveChangesAsync();

        var result = await _service.GetReplayAsync(match.Id, 3, userId);

        Assert.Equal(DealReplayStatus.Ok, result.Status);
        var replay = result.Replay!;
        Assert.Equal(3, replay.DealNumber);
        Assert.Equal(CoreGameMode.AllTrumps, replay.GameMode);
        Assert.Equal(Giretra.Core.GameModes.MultiplierState.Doubled, replay.Multiplier);
        Assert.Equal(CorePosition.Right, replay.Dealer);
        Assert.Equal(CoreTeam.Team1, replay.AnnouncerTeam);
        Assert.Equal(2, replay.Team1ScoreBefore);
        Assert.Equal(3, replay.Team2ScoreBefore);
        Assert.Equal(2, replay.Team1MatchPointsEarned);
        Assert.Equal(0, replay.Team2MatchPointsEarned);
        Assert.Equal(227, replay.Team1CardPoints);
        Assert.Equal(31, replay.Team2CardPoints);
        Assert.Equal(OracleWinners, replay.Tricks.Select(t => t.Winner).ToArray());

        var byPosition = replay.Players.ToDictionary(p => p.Position);
        Assert.Equal("Alice", byPosition[CorePosition.Bottom].DisplayName);
        Assert.False(byPosition[CorePosition.Bottom].IsBot);
        Assert.Equal(player.Id, byPosition[CorePosition.Bottom].PlayerId);
        Assert.Equal("Rakoto", byPosition[CorePosition.Left].DisplayName);
        Assert.True(byPosition[CorePosition.Left].IsBot);
        Assert.Equal(CoreTeam.Team2, byPosition[CorePosition.Left].Team);
    }

    [Fact]
    public async Task NonParticipant_IsForbidden()
    {
        var (_, alice) = AddHuman("Alice");
        var (strangerId, _) = AddHuman("Mallory");
        var match = AddMatch(completed: true);
        AddSeat(match, alice, PlayerPosition.Bottom, Team.Team1);
        AddDeal(match, 1, 2, 0, withPlays: true);
        await _db.SaveChangesAsync();

        var result = await _service.GetReplayAsync(match.Id, 1, strangerId);

        Assert.Equal(DealReplayStatus.Forbidden, result.Status);
        Assert.Null(result.Replay);
    }

    [Fact]
    public async Task UnknownMatchOrDeal_IsNotFound()
    {
        var (userId, player) = AddHuman("Alice");
        var match = AddMatch(completed: true);
        AddSeat(match, player, PlayerPosition.Bottom, Team.Team1);
        AddDeal(match, 1, 2, 0, withPlays: true);
        await _db.SaveChangesAsync();

        Assert.Equal(DealReplayStatus.MatchNotFound, (await _service.GetReplayAsync(Guid.NewGuid(), 1, userId)).Status);
        Assert.Equal(DealReplayStatus.DealNotFound, (await _service.GetReplayAsync(match.Id, 2, userId)).Status);
    }

    [Fact]
    public async Task AbandonedMatch_IsNotReplayable_EvenForCompletedDeals()
    {
        var (userId, player) = AddHuman("Alice");
        var match = AddMatch(completed: true, abandoned: true);
        AddSeat(match, player, PlayerPosition.Bottom, Team.Team1);
        AddDeal(match, 1, 2, 0, withPlays: true);
        await _db.SaveChangesAsync();

        var result = await _service.GetReplayAsync(match.Id, 1, userId);

        Assert.Equal(DealReplayStatus.NotReplayable, result.Status);
    }

    [Fact]
    public async Task DealWithoutFullPlayRecord_IsNotReplayable()
    {
        var (userId, player) = AddHuman("Alice");
        var match = AddMatch(completed: true);
        AddSeat(match, player, PlayerPosition.Bottom, Team.Team1);
        var deal = AddDeal(match, 1, 2, 0, withPlays: true);
        var lastPlay = deal.DealActions.OrderBy(a => a.ActionOrder).Last();
        deal.DealActions.Remove(lastPlay);
        await _db.SaveChangesAsync();

        var result = await _service.GetReplayAsync(match.Id, 1, userId);

        Assert.Equal(DealReplayStatus.NotReplayable, result.Status);
    }

    #endregion

    #region Fixture helpers

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

    private Match AddMatch(bool completed, bool abandoned = false)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            RoomName = "Room",
            TargetScore = 150,
            TotalDeals = 3,
            IsRanked = true,
            WasAbandoned = abandoned,
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

    private Deal AddDeal(Match match, int number, int t1MatchPoints, int t2MatchPoints, bool withPlays,
        MultiplierState multiplier = MultiplierState.Normal)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DealNumber = (short)number,
            DealerPosition = PlayerPosition.Right,
            GameMode = GameMode.AllTrumps,
            AnnouncerTeam = Team.Team1,
            Multiplier = multiplier,
            Team1CardPoints = 227,
            Team2CardPoints = 31,
            Team1MatchPoints = t1MatchPoints,
            Team2MatchPoints = t2MatchPoints,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow,
        };

        if (withPlays)
        {
            // A negotiation action first, to check plays are filtered by type, not by position.
            deal.DealActions.Add(new DealAction
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                ActionOrder = 0,
                ActionType = ActionType.Announce,
                PlayerPosition = PlayerPosition.Bottom,
                GameMode = GameMode.AllTrumps,
            });
            for (var i = 0; i < OraclePlays.Length; i++)
            {
                var (player, rank, suit) = OraclePlays[i];
                deal.DealActions.Add(new DealAction
                {
                    Id = Guid.NewGuid(),
                    DealId = deal.Id,
                    ActionOrder = (short)(i + 1),
                    ActionType = ActionType.PlayCard,
                    PlayerPosition = (PlayerPosition)(int)player,
                    CardRank = (CardRank)(int)rank,
                    CardSuit = (CardSuit)(int)suit,
                    TrickNumber = (short)(i / 4 + 1),
                });
            }
        }

        _db.Deals.Add(deal);
        return deal;
    }

    #endregion
}
