using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;

namespace Giretra.Model.Entities;

public class Friendship
{
    [Key]
    public Guid Id { get; set; }

    public Guid RequesterId { get; set; }

    public Guid AddresseeId { get; set; }

    public FriendshipStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [ForeignKey(nameof(RequesterId))]
    public User Requester { get; set; } = null!;

    [ForeignKey(nameof(AddresseeId))]
    public User Addressee { get; set; } = null!;
}
