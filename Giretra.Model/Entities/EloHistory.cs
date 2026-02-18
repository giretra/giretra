using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(PlayerId), nameof(MatchId), IsUnique = true)]
[Index(nameof(PlayerId), nameof(RecordedAt), IsDescending = [false, true])]
[Index(nameof(MatchId))]
public class EloHistory
{
    [Key]
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Guid MatchId { get; set; }

    public int EloBefore { get; set; }

    public int EloAfter { get; set; }

    public int EloChange { get; set; }

    public bool InvolvedBots { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public Player Player { get; set; } = null!;

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!;
}
