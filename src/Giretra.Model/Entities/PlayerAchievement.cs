using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(PlayerId), nameof(AchievementId), IsUnique = true)]
[Index(nameof(PlayerId), nameof(EarnedAt), IsDescending = [false, true])]
public class PlayerAchievement
{
    [Key]
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Guid AchievementId { get; set; }

    public Guid MatchId { get; set; }

    public short? DealNumber { get; set; }

    public DateTimeOffset EarnedAt { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public Player Player { get; set; } = null!;

    [ForeignKey(nameof(AchievementId))]
    public Achievement Achievement { get; set; } = null!;

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!;
}
