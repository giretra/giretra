using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class ClipConfiguration : IEntityTypeConfiguration<Clip>
{
    public void Configure(EntityTypeBuilder<Clip> builder)
    {
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.Status).HasDefaultValue(ClipStatus.Pending);
        builder.Property(c => c.Locale).HasDefaultValue("en");
        builder.Property(c => c.ViewCount).HasDefaultValue(0);
        builder.Property(c => c.Attempts).HasDefaultValue(0);
        builder.Property(c => c.HasPoster).HasDefaultValue(false);
        builder.Property(c => c.HasVideo).HasDefaultValue(false);
        builder.Property(c => c.ShowAllHands).HasDefaultValue(false);

        builder.HasOne(c => c.Match)
            .WithMany()
            .HasForeignKey(c => c.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedByPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
