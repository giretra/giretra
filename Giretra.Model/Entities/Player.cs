using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;

namespace Giretra.Model.Entities;

public class Player
{
    [Key]
    public Guid Id { get; set; }

    public PlayerType PlayerType { get; set; }

    public Guid? UserId { get; set; }

    public Guid? BotId { get; set; }

    public int EloRating { get; set; }

    public bool EloIsPublic { get; set; }

    public int GamesPlayed { get; set; }

    public int GamesWon { get; set; }

    public int WinStreak { get; set; }

    public int BestWinStreak { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [ForeignKey(nameof(BotId))]
    public Bot? Bot { get; set; }

    public List<MatchPlayer> MatchPlayers { get; set; } = [];

    public List<EloHistory> EloHistories { get; set; } = [];

    public List<Room> CreatedRooms { get; set; } = [];
}
