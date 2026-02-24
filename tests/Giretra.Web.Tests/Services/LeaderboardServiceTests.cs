using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Tests.Services;

public sealed class LeaderboardServiceTests : IDisposable
{
    private readonly GiretraDbContext _db;
    private readonly LeaderboardService _service;

    public LeaderboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<GiretraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new GiretraDbContext(options);
        _service = new LeaderboardService(_db);
    }

    public void Dispose() => _db.Dispose();

    #region Empty state

    [Fact]
    public async Task GetLeaderboard_EmptyDatabase_ReturnsBothListsEmpty()
    {
        var result = await _service.GetLeaderboardAsync();

        Assert.Empty(result.Players);
        Assert.Empty(result.Bots);
        Assert.Equal(0, result.PlayerCount);
        Assert.Equal(0, result.BotCount);
    }

    #endregion

    #region Player ranking

    [Fact]
    public async Task GetLeaderboard_PlayersRankedByRatingDescending()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7);
        AddHuman("Bob", rating: 1900, gamesPlayed: 10, gamesWon: 6);
        AddHuman("Carol", rating: 1700, gamesPlayed: 10, gamesWon: 8);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(3, result.Players.Count);
        Assert.Equal("Bob", result.Players[0].DisplayName);
        Assert.Equal(1, result.Players[0].Rank);
        Assert.Equal("Alice", result.Players[1].DisplayName);
        Assert.Equal(2, result.Players[1].Rank);
        Assert.Equal("Carol", result.Players[2].DisplayName);
        Assert.Equal(3, result.Players[2].Rank);
    }

    [Fact]
    public async Task GetLeaderboard_PlayersTiedByRating_SortedByGamesWon()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 8);
        AddHuman("Bob", rating: 1800, gamesPlayed: 10, gamesWon: 6);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal("Alice", result.Players[0].DisplayName);
        Assert.Equal("Bob", result.Players[1].DisplayName);
    }

    [Fact]
    public async Task GetLeaderboard_PlayersWithFewerThan5Games_Excluded()
    {
        AddHuman("Eligible", rating: 1500, gamesPlayed: 5, gamesWon: 3);
        AddHuman("NotEnough", rating: 1600, gamesPlayed: 4, gamesWon: 4);
        AddHuman("Zero", rating: 1700, gamesPlayed: 0, gamesWon: 0);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Single(result.Players);
        Assert.Equal("Eligible", result.Players[0].DisplayName);
        Assert.Equal(1, result.PlayerCount);
    }

    [Fact]
    public async Task GetLeaderboard_MaxTop100Players()
    {
        for (var i = 0; i < 110; i++)
            AddHuman($"Player{i}", rating: 1000 + i, gamesPlayed: 10, gamesWon: 5);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(100, result.Players.Count);
        Assert.Equal(100, result.PlayerCount);
        // Highest rated should be first
        Assert.Equal("Player109", result.Players[0].DisplayName);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerWinRateCalculation()
    {
        AddHuman("Player", rating: 1500, gamesPlayed: 20, gamesWon: 15);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(75.0, result.Players[0].WinRate);
    }

    [Fact]
    public async Task GetLeaderboard_PlayerWithZeroGames_WinRateIsZero()
    {
        // Edge case: shouldn't appear (< 5 games), but verify the math
        AddHuman("Player", rating: 1500, gamesPlayed: 5, gamesWon: 0);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(0.0, result.Players[0].WinRate);
    }

    #endregion

    #region Player privacy

    [Fact]
    public async Task GetLeaderboard_PrivatePlayer_ShowsAnonymous()
    {
        AddHuman("RealName", rating: 1500, gamesPlayed: 10, gamesWon: 5, eloIsPublic: false);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Single(result.Players);
        Assert.Equal("Anonymous Player", result.Players[0].DisplayName);
        Assert.Null(result.Players[0].AvatarUrl);
    }

    [Fact]
    public async Task GetLeaderboard_PublicPlayer_ShowsRealNameAndAvatar()
    {
        AddHuman("Alice", rating: 1500, gamesPlayed: 10, gamesWon: 5, avatarUrl: "https://example.com/alice.png");
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal("Alice", result.Players[0].DisplayName);
        Assert.Equal("https://example.com/alice.png", result.Players[0].AvatarUrl);
    }

    #endregion

    #region Bot ranking

    [Fact]
    public async Task GetLeaderboard_BotsRankedIndependently()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7);
        AddBot("StrongBot", rating: 1900, gamesPlayed: 50, gamesWon: 30);
        AddBot("WeakBot", rating: 1400, gamesPlayed: 50, gamesWon: 20);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        // Player list has only Alice
        Assert.Single(result.Players);
        Assert.Equal(1, result.Players[0].Rank);

        // Bot list is independent
        Assert.Equal(2, result.Bots.Count);
        Assert.Equal("StrongBot", result.Bots[0].DisplayName);
        Assert.Equal(1, result.Bots[0].Rank);
        Assert.Equal("WeakBot", result.Bots[1].DisplayName);
        Assert.Equal(2, result.Bots[1].Rank);
    }

    [Fact]
    public async Task GetLeaderboard_BotsNoMinimumGamesRequired()
    {
        AddBot("NewBot", rating: 1500, gamesPlayed: 0, gamesWon: 0);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Single(result.Bots);
        Assert.Equal("NewBot", result.Bots[0].DisplayName);
    }

    [Fact]
    public async Task GetLeaderboard_InactiveBots_Excluded()
    {
        AddBot("ActiveBot", rating: 1500, gamesPlayed: 10, gamesWon: 5, isActive: true);
        AddBot("InactiveBot", rating: 1800, gamesPlayed: 10, gamesWon: 8, isActive: false);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Single(result.Bots);
        Assert.Equal("ActiveBot", result.Bots[0].DisplayName);
        Assert.Equal(1, result.BotCount);
    }

    [Fact]
    public async Task GetLeaderboard_BotUsesOwnRating_NotPlayerElo()
    {
        var bot = new Bot
        {
            Id = Guid.NewGuid(),
            AgentType = "TestAgent",
            AgentTypeFactory = "TestFactory",
            DisplayName = "TestBot",
            Rating = 1700,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Bots.Add(bot);

        var player = new Player
        {
            Id = Guid.NewGuid(),
            PlayerType = PlayerType.Bot,
            BotId = bot.Id,
            Bot = bot,
            EloRating = 1200, // Different from Bot.Rating
            GamesPlayed = 10,
            GamesWon = 5,
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(1700, result.Bots[0].Rating);
    }

    [Fact]
    public async Task GetLeaderboard_BotMetadata_AuthorAndDifficulty()
    {
        AddBot("CleverBot", rating: 1600, gamesPlayed: 20, gamesWon: 12,
            author: "John", difficulty: 3);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal("John", result.Bots[0].Author);
        Assert.Equal(3, result.Bots[0].Difficulty);
    }

    [Fact]
    public async Task GetLeaderboard_BotWithNoAuthor_AuthorIsNull()
    {
        AddBot("SimpleBot", rating: 1500, gamesPlayed: 10, gamesWon: 5, author: null);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Null(result.Bots[0].Author);
    }

    [Fact]
    public async Task GetLeaderboard_BotsSortedByRatingThenWinRate()
    {
        AddBot("BotA", rating: 1600, gamesPlayed: 20, gamesWon: 10); // 50% win
        AddBot("BotB", rating: 1600, gamesPlayed: 20, gamesWon: 14); // 70% win
        AddBot("BotC", rating: 1700, gamesPlayed: 20, gamesWon: 8);  // 40% win
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal("BotC", result.Bots[0].DisplayName); // Highest rating
        Assert.Equal("BotB", result.Bots[1].DisplayName); // Same rating as BotA, higher win rate
        Assert.Equal("BotA", result.Bots[2].DisplayName);
    }

    #endregion

    #region Counts

    [Fact]
    public async Task GetLeaderboard_CountsMatchListLengths()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7);
        AddHuman("Bob", rating: 1700, gamesPlayed: 10, gamesWon: 5);
        AddBot("Bot1", rating: 1500, gamesPlayed: 10, gamesWon: 5);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Equal(result.Players.Count, result.PlayerCount);
        Assert.Equal(result.Bots.Count, result.BotCount);
    }

    #endregion

    #region Player profile

    [Fact]
    public async Task GetPlayerProfile_HumanPlayer_ReturnsProfile()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7, avatarUrl: "https://example.com/a.png");
        await _db.SaveChangesAsync();

        var playerId = _db.Players.First(p => p.PlayerType == PlayerType.Human).Id;
        var profile = await _service.GetPlayerProfileAsync(playerId);

        Assert.NotNull(profile);
        Assert.False(profile.IsBot);
        Assert.Equal("Alice", profile.DisplayName);
        Assert.Equal(1800, profile.EloRating);
        Assert.Equal(10, profile.GamesPlayed);
        Assert.Equal(7, profile.GamesWon);
        Assert.Equal("https://example.com/a.png", profile.AvatarUrl);
    }

    [Fact]
    public async Task GetPlayerProfile_PrivateHuman_HidesEloAndAvatar()
    {
        AddHuman("Private", rating: 1800, gamesPlayed: 10, gamesWon: 7,
            eloIsPublic: false, avatarUrl: "https://example.com/p.png");
        await _db.SaveChangesAsync();

        var playerId = _db.Players.First().Id;
        var profile = await _service.GetPlayerProfileAsync(playerId);

        Assert.NotNull(profile);
        Assert.Null(profile.EloRating);
        Assert.Null(profile.AvatarUrl);
    }

    [Fact]
    public async Task GetPlayerProfile_Bot_ReturnsBotProfile()
    {
        AddBot("CleverBot", rating: 1700, gamesPlayed: 50, gamesWon: 30,
            author: "John", difficulty: 3);
        await _db.SaveChangesAsync();

        var playerId = _db.Players.First(p => p.PlayerType == PlayerType.Bot).Id;
        var profile = await _service.GetPlayerProfileAsync(playerId);

        Assert.NotNull(profile);
        Assert.True(profile.IsBot);
        Assert.Equal("CleverBot", profile.DisplayName);
        Assert.Equal(1700, profile.BotRating);
        Assert.Equal("John", profile.Author);
        Assert.Equal((short)3, profile.Difficulty);
        Assert.Equal(50, profile.GamesPlayed);
        Assert.Equal(30, profile.GamesWon);
    }

    [Fact]
    public async Task GetPlayerProfile_NonExistentId_ReturnsNull()
    {
        var profile = await _service.GetPlayerProfileAsync(Guid.NewGuid());

        Assert.Null(profile);
    }

    [Fact]
    public async Task GetLeaderboard_EntriesContainPlayerIds()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7);
        AddBot("Bot1", rating: 1500, gamesPlayed: 10, gamesWon: 5);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.NotEqual(Guid.Empty, result.Players[0].PlayerId);
        Assert.NotEqual(Guid.Empty, result.Bots[0].PlayerId);
    }

    #endregion

    #region Mixed scenarios

    [Fact]
    public async Task GetLeaderboard_OnlyBots_PlayerListEmpty()
    {
        AddBot("Bot1", rating: 1500, gamesPlayed: 10, gamesWon: 5);
        AddBot("Bot2", rating: 1600, gamesPlayed: 10, gamesWon: 6);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Empty(result.Players);
        Assert.Equal(0, result.PlayerCount);
        Assert.Equal(2, result.Bots.Count);
        Assert.Equal(2, result.BotCount);
    }

    [Fact]
    public async Task GetLeaderboard_OnlyPlayers_BotListEmpty()
    {
        AddHuman("Alice", rating: 1800, gamesPlayed: 10, gamesWon: 7);
        await _db.SaveChangesAsync();

        var result = await _service.GetLeaderboardAsync();

        Assert.Single(result.Players);
        Assert.Equal(1, result.PlayerCount);
        Assert.Empty(result.Bots);
        Assert.Equal(0, result.BotCount);
    }

    #endregion

    #region Helpers

    private void AddHuman(
        string displayName,
        int rating,
        int gamesPlayed,
        int gamesWon,
        bool eloIsPublic = true,
        string? avatarUrl = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = Guid.NewGuid(),
            Username = displayName.ToLowerInvariant(),
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
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
            EloRating = rating,
            EloIsPublic = eloIsPublic,
            GamesPlayed = gamesPlayed,
            GamesWon = gamesWon,
        };
        _db.Players.Add(player);
    }

    private void AddBot(
        string displayName,
        int rating,
        int gamesPlayed,
        int gamesWon,
        bool isActive = true,
        string? author = null,
        short difficulty = 1)
    {
        var bot = new Bot
        {
            Id = Guid.NewGuid(),
            AgentType = $"Agent_{displayName}",
            AgentTypeFactory = $"Factory_{displayName}",
            DisplayName = displayName,
            Rating = rating,
            IsActive = isActive,
            Author = author,
            Difficulty = difficulty,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Bots.Add(bot);

        var player = new Player
        {
            Id = Guid.NewGuid(),
            PlayerType = PlayerType.Bot,
            BotId = bot.Id,
            Bot = bot,
            EloRating = rating,
            GamesPlayed = gamesPlayed,
            GamesWon = gamesWon,
        };
        _db.Players.Add(player);
    }

    #endregion
}
