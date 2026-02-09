using System.ComponentModel.DataAnnotations;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(CompletedAt), IsDescending = [true])]
public class Match
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string RoomName { get; set; } = null!;

    public int TargetScore { get; set; }

    public int Team1FinalScore { get; set; }

    public int Team2FinalScore { get; set; }

    public Team? WinnerTeam { get; set; }

    public int TotalDeals { get; set; }

    public bool IsRanked { get; set; }

    public bool WasAbandoned { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<MatchPlayer> MatchPlayers { get; set; } = [];

    public List<Deal> Deals { get; set; } = [];

    public List<EloHistory> EloHistories { get; set; } = [];

    public Room? Room { get; set; }
}
