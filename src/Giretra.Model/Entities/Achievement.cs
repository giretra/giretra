using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(Code), IsUnique = true)]
public class Achievement
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Code { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Category { get; set; } = null!;

    public int Tier { get; set; }

    [MaxLength(100)]
    public string? IconName { get; set; }

    public bool IsHidden { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<PlayerAchievement> PlayerAchievements { get; set; } = [];
}
