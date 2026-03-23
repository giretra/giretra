using Giretra.Model;
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
