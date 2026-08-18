using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Web.Achievements;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AchievementsController : ControllerBase
{
    /// <summary>
    /// Gets all available achievements (active definitions).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AchievementResponse>>> GetAll(
        [FromServices] GiretraDbContext? db = null)
    {
        if (db == null)
            return Ok(new List<AchievementResponse>());

        var achievements = await db.Achievements
            .Where(a => a.IsActive)
            .OrderBy(a => a.Category)
            .ThenBy(a => a.SortOrder)
            .Select(a => new AchievementResponse
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                Category = a.Category,
                Tier = a.Tier,
                IconName = a.IconName,
                IsHidden = a.IsHidden
            })
            .ToListAsync();

        return Ok(achievements);
    }

    /// <summary>
    /// Gets achievements earned by a specific player.
    /// </summary>
    [HttpGet("players/{playerId:guid}")]
    public async Task<ActionResult<List<PlayerAchievementResponse>>> GetPlayerAchievements(
        Guid playerId,
        [FromServices] GiretraDbContext? db = null)
    {
        if (db == null)
            return Ok(new List<PlayerAchievementResponse>());

        var playerExists = await db.Players.AnyAsync(p => p.Id == playerId);
        if (!playerExists)
            return NotFound();

        var achievements = await db.PlayerAchievements
            .Where(pa => pa.PlayerId == playerId)
            .Join(db.Achievements, pa => pa.AchievementId, a => a.Id, (pa, a) => new { pa, a })
            .OrderByDescending(x => x.pa.EarnedAt)
            .Select(x => new PlayerAchievementResponse
            {
                Code = x.a.Code,
                Name = x.a.Name,
                Category = x.a.Category,
                Tier = x.a.Tier,
                IconName = x.a.IconName,
                IsHidden = x.a.IsHidden,
                EarnedAt = x.pa.EarnedAt,
                MatchId = x.pa.MatchId,
                DealNumber = x.pa.DealNumber
            })
            .ToListAsync();

        return Ok(achievements);
    }

    /// <summary>
    /// Gets all achievements with earned status for a specific player (public profile).
    /// </summary>
    [HttpGet("showcase/{playerId:guid}")]
    public async Task<ActionResult<AchievementShowcaseResponse>> GetShowcase(
        Guid playerId,
        [FromServices] AiPlayerRegistry aiRegistry,
        [FromServices] GiretraDbContext? db = null)
    {
        if (db == null)
            return Ok(new AchievementShowcaseResponse { PlayerName = "", Achievements = [], QualifyingBots = QualifyingBots(aiRegistry) });

        var player = await db.Players
            .Include(p => p.User)
            .Include(p => p.Bot)
            .FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null)
            return NotFound();

        var playerName = player.User?.EffectiveDisplayName ?? player.Bot?.DisplayName ?? "Unknown";

        return Ok(await BuildShowcase(db, playerId, playerName, aiRegistry));
    }

    /// <summary>
    /// Gets all achievements with earned status for the current authenticated user.
    /// </summary>
    [HttpGet("showcase/me")]
    [Authorize]
    public async Task<ActionResult<AchievementShowcaseResponse>> GetMyShowcase(
        [FromServices] AiPlayerRegistry aiRegistry,
        [FromServices] GiretraDbContext? db = null)
    {
        if (db == null)
            return Ok(new AchievementShowcaseResponse { PlayerName = "", Achievements = [], QualifyingBots = QualifyingBots(aiRegistry) });

        var user = (User)HttpContext.Items["GiretraUser"]!;
        var player = await db.Players.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (player == null)
            return Ok(new AchievementShowcaseResponse { PlayerName = user.EffectiveDisplayName, Achievements = [], QualifyingBots = QualifyingBots(aiRegistry) });

        return Ok(await BuildShowcase(db, player.Id, user.EffectiveDisplayName, aiRegistry));
    }

    private static List<string> QualifyingBots(AiPlayerRegistry aiRegistry) =>
        aiRegistry.GetAvailableTypes()
            .Where(t => t.Rating >= AchievementEvaluator.MinOpponentRating)
            .OrderByDescending(t => t.Rating)
            .Select(t => t.DisplayName)
            .ToList();

    private static async Task<AchievementShowcaseResponse> BuildShowcase(
        GiretraDbContext db, Guid playerId, string playerName, AiPlayerRegistry aiRegistry)
    {
        var allAchievements = await db.Achievements
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.Tier)
            .ThenBy(a => a.Category)
            .ThenBy(a => a.SortOrder)
            .ToListAsync();

        var earnedSet = await db.PlayerAchievements
            .Where(pa => pa.PlayerId == playerId)
            .ToDictionaryAsync(pa => pa.AchievementId, pa => pa.EarnedAt);

        var items = allAchievements.Select(a => new AchievementShowcaseItem
        {
            Code = a.Code,
            Name = a.Name,
            Category = a.Category,
            Tier = a.Tier,
            IconName = a.IconName,
            IsHidden = a.IsHidden,
            IsEarned = earnedSet.ContainsKey(a.Id),
            EarnedAt = earnedSet.TryGetValue(a.Id, out var dt) ? dt : null
        }).ToList();

        return new AchievementShowcaseResponse
        {
            PlayerName = playerName,
            EarnedCount = items.Count(i => i.IsEarned),
            TotalCount = items.Count,
            Achievements = items,
            QualifyingBots = QualifyingBots(aiRegistry)
        };
    }
}

public sealed class AchievementShowcaseResponse
{
    public required string PlayerName { get; init; }
    public int EarnedCount { get; init; }
    public int TotalCount { get; init; }
    public required List<AchievementShowcaseItem> Achievements { get; init; }
    public List<string> QualifyingBots { get; init; } = [];
}

public sealed class AchievementShowcaseItem
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
    public required bool IsEarned { get; init; }
    public DateTimeOffset? EarnedAt { get; init; }
}

public sealed class AchievementResponse
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
}

public sealed class PlayerAchievementResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int Tier { get; init; }
    public string? IconName { get; init; }
    public required bool IsHidden { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
    public required Guid MatchId { get; init; }
    public short? DealNumber { get; init; }
}
