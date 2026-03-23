using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Model;
using Giretra.Web.Domain;
using Giretra.Web.Players;
using Giretra.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Achievements;

/// <summary>
/// Evaluates all achievement rules for human players.
/// Called once at match completion — iterates over all completed deals for deal-level rules,
/// then evaluates match-level rules. Persists all newly earned achievements in the same transaction.
/// </summary>
public sealed class AchievementEvaluator
{
    private readonly IEnumerable<IAchievementRule> _rules;
    private readonly AiPlayerRegistry _aiRegistry;
    private readonly ILogger<AchievementEvaluator> _logger;

    public AchievementEvaluator(
        IEnumerable<IAchievementRule> rules,
        AiPlayerRegistry aiRegistry,
        ILogger<AchievementEvaluator> logger)
    {
        _rules = rules;
        _aiRegistry = aiRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all achievements for a completed match. Called from MatchPersistenceService
    /// after deals are persisted, within the same transaction.
    /// </summary>
    public async Task<List<EarnedAchievementInfo>> EvaluateMatchAsync(
        GiretraDbContext db,
        Guid matchId,
        GameSession session,
        MatchState matchState)
    {
        var allEarned = new List<EarnedAchievementInfo>();

        var dealRules = _rules.Where(r => r.Trigger.HasFlag(AchievementTrigger.DealEnd)).ToList();
        var matchRules = _rules.Where(r => r.Trigger.HasFlag(AchievementTrigger.MatchEnd)).ToList();

        if (dealRules.Count == 0 && matchRules.Count == 0)
            return allEarned;

        var recordedDeals = session.ActionRecorder?.GetDeals() ?? [];

        // Resolve eligible human players once
        var eligiblePlayers = await ResolveEligiblePlayersAsync(db, session);
        if (eligiblePlayers.Count == 0)
            return allEarned;

        // Load already-earned codes per player (once, updated as we go)
        var earnedSets = new Dictionary<PlayerPosition, HashSet<string>>();
        foreach (var (position, playerId) in eligiblePlayers)
        {
            var codes = await db.PlayerAchievements
                .Where(pa => pa.PlayerId == playerId)
                .Join(db.Achievements, pa => pa.AchievementId, a => a.Id, (_, a) => a.Code)
                .ToListAsync();
            earnedSets[position] = codes.ToHashSet();
        }

        // Pre-load achievement entities by code for quick lookup
        var achievementsByCode = await db.Achievements
            .Where(a => a.IsActive)
            .ToDictionaryAsync(a => a.Code);

        // Pass 1: evaluate deal-level rules for each completed deal
        if (dealRules.Count > 0)
        {
            for (var i = 0; i < matchState.CompletedDeals.Count; i++)
            {
                var dealResult = matchState.CompletedDeals[i];
                var dealNumber = (short)(i + 1);
                var recordedDeal = recordedDeals.FirstOrDefault(rd => rd.DealNumber == dealNumber);

                foreach (var (position, playerId) in eligiblePlayers)
                {
                    var context = BuildContext(
                        AchievementTrigger.DealEnd, dealResult, dealNumber,
                        matchState, position, playerId, recordedDeal, earnedSets[position]);

                    var newlyEarned = await EvaluateRulesAsync(
                        db, dealRules, context, matchId, dealNumber,
                        achievementsByCode, earnedSets[position]);

                    allEarned.AddRange(newlyEarned);
                }
            }
        }

        // Pass 2: evaluate match-level rules
        if (matchRules.Count > 0)
        {
            var lastRecordedDeal = recordedDeals.LastOrDefault();

            foreach (var (position, playerId) in eligiblePlayers)
            {
                var context = BuildContext(
                    AchievementTrigger.MatchEnd, null, null,
                    matchState, position, playerId, lastRecordedDeal, earnedSets[position]);

                var newlyEarned = await EvaluateRulesAsync(
                    db, matchRules, context, matchId, null,
                    achievementsByCode, earnedSets[position]);

                allEarned.AddRange(newlyEarned);
            }
        }

        return allEarned;
    }

    private async Task<List<EarnedAchievementInfo>> EvaluateRulesAsync(
        GiretraDbContext db,
        List<IAchievementRule> rules,
        AchievementContext context,
        Guid matchId,
        short? dealNumber,
        Dictionary<string, Model.Entities.Achievement> achievementsByCode,
        HashSet<string> earnedSet)
    {
        var earned = new List<EarnedAchievementInfo>();

        foreach (var rule in rules)
        {
            if (earnedSet.Contains(rule.Code))
                continue;

            try
            {
                if (!await rule.IsEarnedAsync(context))
                    continue;

                if (!achievementsByCode.TryGetValue(rule.Code, out var achievement))
                {
                    _logger.LogWarning(
                        "Achievement rule {Code} earned but not found in DB", rule.Code);
                    continue;
                }

                db.PlayerAchievements.Add(new Model.Entities.PlayerAchievement
                {
                    Id = Guid.NewGuid(),
                    PlayerId = context.PlayerId,
                    AchievementId = achievement.Id,
                    MatchId = matchId,
                    DealNumber = dealNumber,
                    EarnedAt = DateTimeOffset.UtcNow
                });

                earnedSet.Add(rule.Code);

                earned.Add(new EarnedAchievementInfo(
                    context.PlayerPosition, rule.Code, rule.Name,
                    rule.Category, rule.Tier, rule.IconName, rule.IsHidden, dealNumber));

                _logger.LogInformation(
                    "Player {PlayerId} earned achievement {Code} at position {Position}",
                    context.PlayerId, rule.Code, context.PlayerPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error evaluating achievement {Code} for player {PlayerId}",
                    rule.Code, context.PlayerId);
            }
        }

        return earned;
    }

    private async Task<Dictionary<PlayerPosition, Guid>> ResolveEligiblePlayersAsync(
        GiretraDbContext db, GameSession session)
    {
        var result = new Dictionary<PlayerPosition, Guid>();

        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var info = session.PlayerComposition[position];
            if (info.IsBot || !info.UserId.HasValue)
                continue;

            if (!IsEligible(session, position))
                continue;

            var player = await db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == info.UserId.Value);

            if (player != null)
                result[position] = player.Id;
        }

        return result;
    }

    private bool IsEligible(GameSession session, PlayerPosition position)
    {
        if (!session.IsRanked)
            return false;

        var team = position.GetTeam();
        var opponentPositions = team == Team.Team1
            ? new[] { PlayerPosition.Left, PlayerPosition.Right }
            : new[] { PlayerPosition.Bottom, PlayerPosition.Top };

        foreach (var opp in opponentPositions)
        {
            var oppInfo = session.PlayerComposition[opp];
            if (!oppInfo.IsBot || oppInfo.AiAgentType == null)
                return false;

            var rating = _aiRegistry.GetRating(oppInfo.AiAgentType);
            if (rating == null || rating < 1400)
                return false;
        }

        return true;
    }

    private static AchievementContext BuildContext(
        AchievementTrigger trigger,
        DealResult? dealResult,
        short? dealNumber,
        MatchState matchState,
        PlayerPosition position,
        Guid playerId,
        RecordedDeal? recordedDeal,
        IReadOnlySet<string> alreadyEarned)
    {
        var initialHand = recordedDeal?.InitialHands.GetValueOrDefault(position)
            ?? (IReadOnlyList<Core.Cards.Card>)[];
        var fullHand = recordedDeal?.FullHands.GetValueOrDefault(position)
            ?? (IReadOnlyList<Core.Cards.Card>)[];

        var negotiationActions = recordedDeal?.Actions
            .Where(a => a.ActionType is RecordedActionType.Announce
                or RecordedActionType.Accept
                or RecordedActionType.Double
                or RecordedActionType.Redouble
                or RecordedActionType.ReRedouble)
            .ToList() ?? [];

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
            AlreadyEarnedCodes = alreadyEarned
        };
    }
}
