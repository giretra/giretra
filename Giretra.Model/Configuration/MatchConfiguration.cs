using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.TargetScore).HasDefaultValue(150);
        builder.Property(m => m.IsRanked).HasDefaultValue(true);
        builder.Property(m => m.WasAbandoned).HasDefaultValue(false);
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(m => m.IsRanked).HasFilter("is_ranked = TRUE");
    }
}
