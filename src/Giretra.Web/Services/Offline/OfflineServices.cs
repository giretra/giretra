using System.Collections.Concurrent;
using System.Security.Claims;
using Giretra.Core.Cards;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Model.Entities;
using Giretra.Web.Achievements;
using Giretra.Web.Domain;
using Giretra.Web.Models.Responses;
using Giretra.Web.Players;
using Giretra.Web.Services.Elo;
using UserRole = Giretra.Model.Enums.UserRole;

namespace Giretra.Web.Services.Offline;

/// <summary>
/// In-memory user sync that creates User entities on the fly (no database).
/// </summary>
public sealed class OfflineUserSyncService : IUserSyncService
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User> SyncUserAsync(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No sub claim found");

        var userId = Guid.Parse(sub);
        var username = principal.FindFirstValue("preferred_username") ?? "offline";
        var name = principal.FindFirstValue("name") ?? username;
        var email = principal.FindFirstValue("email");

        var user = _users.GetOrAdd(userId, _ => new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = userId,
            Username = username,
            DisplayName = name,
            Email = email,
            Role = UserRole.Normal,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        // Update last login
        user.LastLoginAt = DateTimeOffset.UtcNow;

        return Task.FromResult(user);
    }

    /// <summary>
    /// Looks up a previously synced user by their Keycloak (deterministic) GUID.
    /// </summary>
    public User? FindByKeycloakId(Guid keycloakId)
        => _users.TryGetValue(keycloakId, out var user) ? user : null;
}

/// <summary>
/// Offline match persistence: no database writes but evaluates achievements in-memory.
/// Skips ranked/rating eligibility checks so achievements can be tested offline.
/// </summary>
public sealed class OfflineMatchPersistenceService : IMatchPersistenceService
{
    private readonly IEnumerable<IAchievementRule> _rules;
    private readonly ILogger<OfflineMatchPersistenceService> _logger;
    private readonly HashSet<string> _earnedCodes = [];

    public OfflineMatchPersistenceService(
        IEnumerable<IAchievementRule> rules,
        ILogger<OfflineMatchPersistenceService> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task PersistCompletedMatchAsync(GameSession session)
    {
        var matchState = session.MatchState;
        if (matchState == null) return;

        var dealRules = _rules.Where(r => r.Trigger.HasFlag(AchievementTrigger.DealEnd)).ToList();
        var matchRules = _rules.Where(r => r.Trigger.HasFlag(AchievementTrigger.MatchEnd)).ToList();
        if (dealRules.Count == 0 && matchRules.Count == 0) return;

        var recordedDeals = session.ActionRecorder?.GetDeals() ?? [];

        // Find human player positions (no DB lookup — use a deterministic fake ID)
        var humanPlayers = new Dictionary<PlayerPosition, Guid>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var info = session.PlayerComposition[position];
            if (!info.IsBot)
                humanPlayers[position] = info.UserId ?? Guid.NewGuid();
        }

        if (humanPlayers.Count == 0) return;

        // Pass 1: deal-level rules
        foreach (var (i, dealResult) in matchState.CompletedDeals.Select((d, i) => (i, d)))
        {
            var dealNumber = (short)(i + 1);
            var recordedDeal = recordedDeals.FirstOrDefault(rd => rd.DealNumber == dealNumber);

            foreach (var (position, playerId) in humanPlayers)
            {
                var context = BuildContext(
                    AchievementTrigger.DealEnd, dealResult, dealNumber,
                    matchState, position, playerId, recordedDeal, _earnedCodes);

                await EvaluateRulesAsync(dealRules, context, dealNumber, session);
            }
        }

        // Pass 2: match-level rules
        var lastRecordedDeal = recordedDeals.LastOrDefault();
        foreach (var (position, playerId) in humanPlayers)
        {
            var context = BuildContext(
                AchievementTrigger.MatchEnd, null, null,
                matchState, position, playerId, lastRecordedDeal, _earnedCodes);

            await EvaluateRulesAsync(matchRules, context, null, session);
        }

        if (session.EarnedAchievements.Count > 0)
            _logger.LogInformation("Offline: {Count} achievement(s) earned", session.EarnedAchievements.Count);
    }

    public Task PersistAbandonedMatchAsync(GameSession session, PlayerPosition abandonerPosition) => Task.CompletedTask;

    private async Task EvaluateRulesAsync(
        List<IAchievementRule> rules, AchievementContext context, short? dealNumber, GameSession session)
    {
        foreach (var rule in rules)
        {
            if (_earnedCodes.Contains(rule.Code)) continue;

            try
            {
                if (!await rule.IsEarnedAsync(context)) continue;

                _earnedCodes.Add(rule.Code);
                session.EarnedAchievements.Add(new EarnedAchievementInfo(
                    context.PlayerPosition, rule.Code, rule.Name,
                    rule.Category, rule.Tier, rule.IconName, rule.IsHidden, dealNumber));

                _logger.LogInformation("Offline: achievement {Code} earned at position {Position}",
                    rule.Code, context.PlayerPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating offline achievement {Code}", rule.Code);
            }
        }
    }

    private static AchievementContext BuildContext(
        AchievementTrigger trigger, Core.Scoring.DealResult? dealResult, short? dealNumber,
        Core.State.MatchState matchState, PlayerPosition position, Guid playerId,
        RecordedDeal? recordedDeal, IReadOnlySet<string> alreadyEarned)
    {
        var initialHand = recordedDeal?.InitialHands.GetValueOrDefault(position)
            ?? (IReadOnlyList<Card>)[];
        var fullHand = recordedDeal?.FullHands.GetValueOrDefault(position)
            ?? (IReadOnlyList<Card>)[];

        var negotiationActions = recordedDeal?.Actions
            .Where(a => a.ActionType is RecordedActionType.Announce
                or RecordedActionType.Accept
                or RecordedActionType.Double
                or RecordedActionType.Redouble
                or RecordedActionType.ReRedouble)
            .ToList() ?? [];

        var tricks = BuildTricks(recordedDeal, dealResult);

        return new AchievementContext
        {
            Trigger = trigger,
            DealResult = dealResult,
            DealNumber = dealNumber,
            MatchState = matchState,
            CompletedDeals = matchState.CompletedDeals,
            PlayerPosition = position,
            PlayerTeam = position.GetTeam(),
            PlayerId = playerId,
            InitialHand = initialHand,
            FullHand = fullHand,
            NegotiationActions = negotiationActions,
            Tricks = tricks,
            AlreadyEarnedCodes = alreadyEarned
        };
    }

    private static List<CompletedTrick> BuildTricks(RecordedDeal? recordedDeal, Core.Scoring.DealResult? dealResult)
    {
        if (recordedDeal == null || dealResult == null) return [];

        var gameMode = dealResult.GameMode;
        var playActions = recordedDeal.Actions
            .Where(a => a.ActionType is RecordedActionType.PlayCard)
            .GroupBy(a => a.TrickNumber)
            .OrderBy(g => g.Key);

        var tricks = new List<CompletedTrick>();
        foreach (var group in playActions)
        {
            var plays = group.OrderBy(a => a.ActionOrder).ToList();
            if (plays.Count != 4) continue;

            var playedCards = plays
                .Select(a => new PlayedCard(a.PlayerPosition, new Card(a.CardRank!.Value, a.CardSuit!.Value)))
                .ToList();

            var leadSuit = playedCards[0].Card.Suit;
            var winnerIndex = 0;
            for (var j = 1; j < playedCards.Count; j++)
            {
                if (CardComparer.Beats(playedCards[j].Card, playedCards[winnerIndex].Card, leadSuit, gameMode))
                    winnerIndex = j;
            }

            tricks.Add(new CompletedTrick(
                group.Key!.Value, playedCards, leadSuit, playedCards[winnerIndex].Player));
        }

        return tricks;
    }
}

/// <summary>
/// No-op ELO service (no database to update).
/// </summary>
public sealed class OfflineEloService : IEloService
{
    public Task StageMatchEloAsync(Guid matchId, GameSession session) => Task.CompletedTask;
    public Task StageAbandonEloAsync(Guid matchId, GameSession session, PlayerPosition abandonerPosition) => Task.CompletedTask;
    public Task<IReadOnlyDictionary<PlayerPosition, EloChangePreview>?> PreviewMatchEloAsync(GameSession session)
        => Task.FromResult<IReadOnlyDictionary<PlayerPosition, EloChangePreview>?>(null);
}

/// <summary>
/// Stub profile service returning minimal data from in-memory user.
/// </summary>
public sealed class OfflineProfileService : IProfileService
{
    private readonly OfflineUserSyncService _userSync;

    public OfflineProfileService(OfflineUserSyncService userSync)
    {
        _userSync = userSync;
    }

    public Task<ProfileResponse> GetProfileAsync(Guid userId)
    {
        // Try to find a user that was synced with this keycloak ID
        var user = _userSync.FindByKeycloakId(userId);

        return Task.FromResult(new ProfileResponse
        {
            Username = user?.Username ?? "offline",
            DisplayName = user?.EffectiveDisplayName ?? "Offline User",
            EloRating = 1000,
            EloIsPublic = true,
            GamesPlayed = 0,
            GamesWon = 0,
            WinStreak = 0,
            BestWinStreak = 0,
            CreatedAt = user?.CreatedAt ?? DateTimeOffset.UtcNow,
        });
    }

    public Task<PlayerProfileResponse?> GetPlayerProfileAsync(string roomId, PlayerPosition position, Guid requestingUserId)
        => Task.FromResult<PlayerProfileResponse?>(null);

    public Task<(bool Success, string? Error)> UpdateDisplayNameAsync(Guid userId, string displayName)
        => Task.FromResult((true, (string?)null));

    public Task<(bool Success, string? AvatarUrl, string? Error)> UpdateAvatarAsync(Guid userId, IFormFile file)
        => Task.FromResult((false, (string?)null, (string?)"Not available offline"));

    public Task DeleteAvatarAsync(Guid userId) => Task.CompletedTask;

    public Task UpdateEloVisibilityAsync(Guid userId, bool isPublic) => Task.CompletedTask;
}

/// <summary>
/// Stub friend service returning empty data.
/// </summary>
public sealed class OfflineFriendService : IFriendService
{
    public Task<FriendsListResponse> GetFriendsAsync(Guid userId)
        => Task.FromResult(new FriendsListResponse
        {
            Friends = [],
            PendingReceived = [],
            PendingSent = [],
        });

    public Task<(bool Success, string? Error, Guid? AffectedUserId)> SendFriendRequestAsync(Guid userId, string username)
        => Task.FromResult((false, (string?)"Not available offline", (Guid?)null));

    public Task<(bool Success, string? Error)> AcceptFriendRequestAsync(Guid userId, Guid friendshipId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> DeclineFriendRequestAsync(Guid userId, Guid friendshipId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> RemoveFriendAsync(Guid userId, Guid friendUserId)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<UserSearchResponse> SearchUsersAsync(Guid userId, string query)
        => Task.FromResult(new UserSearchResponse { Results = [] });

    public Task<int> GetPendingCountAsync(Guid userId)
        => Task.FromResult(0);
}

/// <summary>
/// Stub block service returning empty data.
/// </summary>
public sealed class OfflineBlockService : IBlockService
{
    public Task<IReadOnlyList<BlockedUserResponse>> GetBlockedUsersAsync(Guid userId)
        => Task.FromResult<IReadOnlyList<BlockedUserResponse>>([]);

    public Task<(bool Success, string? Error)> BlockUserAsync(Guid userId, string username, string? reason)
        => Task.FromResult((false, (string?)"Not available offline"));

    public Task<(bool Success, string? Error)> UnblockUserAsync(Guid userId, Guid blockId)
        => Task.FromResult((false, (string?)"Not available offline"));
}

/// <summary>
/// Stub match history service returning empty data.
/// </summary>
public sealed class OfflineMatchHistoryService : IMatchHistoryService
{
    public Task<MatchHistoryListResponse> GetMatchHistoryAsync(Guid userId, int page, int pageSize)
        => Task.FromResult(new MatchHistoryListResponse
        {
            Matches = [],
            TotalCount = 0,
            Page = page,
            PageSize = pageSize,
        });
}

/// <summary>
/// Stub leaderboard service returning empty data.
/// </summary>
public sealed class OfflineLeaderboardService : ILeaderboardService
{
    public Task<LeaderboardResponse> GetLeaderboardAsync()
        => Task.FromResult(new LeaderboardResponse
        {
            Players = [],
            Bots = [],
            PlayerCount = 0,
            BotCount = 0,
        });

    public Task<PlayerProfileResponse?> GetPlayerProfileAsync(Guid playerId)
        => Task.FromResult<PlayerProfileResponse?>(null);
}

/// <summary>
/// Extension methods to register all offline service stubs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOfflineServices(this IServiceCollection services)
    {
        // OfflineUserSyncService is registered as singleton so OfflineProfileService can access it
        var userSync = new OfflineUserSyncService();
        services.AddSingleton(userSync);
        services.AddSingleton<IUserSyncService>(userSync);

        // Achievement rules (same discovery as online mode, no DB sync needed)
        var achievementRuleTypes = typeof(IAchievementRule).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && typeof(IAchievementRule).IsAssignableFrom(t));
        foreach (var type in achievementRuleTypes)
            services.AddSingleton(typeof(IAchievementRule), type);

        services.AddSingleton<IMatchPersistenceService, OfflineMatchPersistenceService>();
        services.AddSingleton<IEloService, OfflineEloService>();
        services.AddSingleton<IProfileService>(sp => new OfflineProfileService(sp.GetRequiredService<OfflineUserSyncService>()));
        services.AddSingleton<IFriendService, OfflineFriendService>();
        services.AddSingleton<IBlockService, OfflineBlockService>();
        services.AddSingleton<IMatchHistoryService, OfflineMatchHistoryService>();
        services.AddSingleton<ILeaderboardService, OfflineLeaderboardService>();

        return services;
    }
}
