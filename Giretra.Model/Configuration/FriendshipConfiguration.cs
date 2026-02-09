using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(f => f.Status)
            .HasMaxLength(10)
            .HasDefaultValue(FriendshipStatus.Pending)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<FriendshipStatus>(v, ignoreCase: true));

        builder.Property(f => f.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(f => f.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(f => f.Requester)
            .WithMany(u => u.SentFriendRequests)
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Addressee)
            .WithMany(u => u.ReceivedFriendRequests)
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_no_self_friend",
            "requester_id != addressee_id"));

        // NOTE: The unique constraint UNIQUE(LEAST(requester_id, addressee_id), GREATEST(requester_id, addressee_id))
        // cannot be expressed in EF Core Fluent API. This must be added via raw SQL in the first migration:
        // CREATE UNIQUE INDEX uq_friendship_pair ON friendships (LEAST(requester_id, addressee_id), GREATEST(requester_id, addressee_id));

        builder.HasIndex(f => f.RequesterId).HasFilter("status = 'accepted'");
        builder.HasIndex(f => f.AddresseeId).HasFilter("status = 'pending'");
    }
}
