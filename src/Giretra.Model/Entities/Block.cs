using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(BlockerId), nameof(BlockedId), IsUnique = true)]
[Index(nameof(BlockerId))]
public class Block
{
    [Key]
    public Guid Id { get; set; }

    public Guid BlockerId { get; set; }

    public Guid BlockedId { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(BlockerId))]
    public User Blocker { get; set; } = null!;

    [ForeignKey(nameof(BlockedId))]
    public User Blocked { get; set; } = null!;
}
