using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.Property(d => d.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(d => d.Multiplier).HasDefaultValue(MultiplierState.Normal);
        builder.Property(d => d.WasSweep).HasDefaultValue(false);
        builder.Property(d => d.IsInstantWin).HasDefaultValue(false);

        builder.HasOne(d => d.Match)
            .WithMany(m => m.Deals)
            .HasForeignKey(d => d.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
