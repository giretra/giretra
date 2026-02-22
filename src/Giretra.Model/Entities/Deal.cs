using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(MatchId), nameof(DealNumber), IsUnique = true)]
[Index(nameof(MatchId))]
public class Deal
{
    [Key]
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public short DealNumber { get; set; }

    public PlayerPosition DealerPosition { get; set; }

    public GameMode? GameMode { get; set; }

    public Team? AnnouncerTeam { get; set; }

    public MultiplierState Multiplier { get; set; }

    public int? Team1CardPoints { get; set; }

    public int? Team2CardPoints { get; set; }

    public int? Team1MatchPoints { get; set; }

    public int? Team2MatchPoints { get; set; }

    public bool WasSweep { get; set; }

    public Team? SweepingTeam { get; set; }

    public bool IsInstantWin { get; set; }

    public bool? AnnouncerWon { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!;

    public List<DealAction> DealActions { get; set; } = [];
}
