using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(CreatorPlayerId))]
public class Room
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    public Guid CreatorPlayerId { get; set; }

    public Guid? MatchId { get; set; }

    public int TurnTimerSeconds { get; set; }

    public short PlayerCount { get; set; }

    public short BotCount { get; set; }

    public short WatcherCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ClosedAt { get; set; }

    [ForeignKey(nameof(CreatorPlayerId))]
    public Player CreatorPlayer { get; set; } = null!;

    [ForeignKey(nameof(MatchId))]
    public Match? Match { get; set; }
}
