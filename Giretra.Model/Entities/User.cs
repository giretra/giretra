using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(KeycloakId), IsUnique = true)]
[Index(nameof(Username), IsUnique = true)]
public class User
{
    [Key]
    public Guid Id { get; set; }

    public Guid KeycloakId { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = null!;

    [MaxLength(255)]
    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; }

    public bool IsBanned { get; set; }

    public string? BanReason { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Player? Player { get; set; }

    [InverseProperty(nameof(Friendship.Requester))]
    public List<Friendship> SentFriendRequests { get; set; } = [];

    [InverseProperty(nameof(Friendship.Addressee))]
    public List<Friendship> ReceivedFriendRequests { get; set; } = [];

    [InverseProperty(nameof(Block.Blocker))]
    public List<Block> BlocksInitiated { get; set; } = [];

    [InverseProperty(nameof(Block.Blocked))]
    public List<Block> BlocksReceived { get; set; } = [];
}
