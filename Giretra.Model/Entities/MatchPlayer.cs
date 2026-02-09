using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(MatchId), nameof(Position), IsUnique = true)]
[Index(nameof(MatchId), nameof(PlayerId), IsUnique = true)]
[Index(nameof(PlayerId))]
[Index(nameof(MatchId))]
public class MatchPlayer
{
    [Key]
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Guid PlayerId { get; set; }

    public PlayerPosition Position { get; set; }

    public Team Team { get; set; }

    public bool IsWinner { get; set; }

    public int? EloBefore { get; set; }

    public int? EloAfter { get; set; }

    public int? EloChange { get; set; }

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!;

    [ForeignKey(nameof(PlayerId))]
    public Player Player { get; set; } = null!;
}
