using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        // Room uses the same UUID as the in-memory room, no auto-generation
        builder.Property(r => r.WatcherCount).HasDefaultValue((short)0);

        builder.HasOne(r => r.CreatorPlayer)
            .WithMany(p => p.CreatedRooms)
            .HasForeignKey(r => r.CreatorPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Match)
            .WithOne(m => m.Room)
            .HasForeignKey<Room>(r => r.MatchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.MatchId).HasFilter("match_id IS NOT NULL");
    }
}
