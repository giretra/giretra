using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(AgentType), IsUnique = true)]
public class Bot
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string AgentType { get; set; } = null!;

    [Required, MaxLength(255)]
    public string AgentTypeFactory { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    [MaxLength(255)]
    public string? Author { get; set; }

    [MaxLength(512)]
    public string? Pun { get; set; }

    public short Difficulty { get; set; }

    public int Rating { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Player? Player { get; set; }
}
