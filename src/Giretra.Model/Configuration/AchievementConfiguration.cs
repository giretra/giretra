using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.IsActive).HasDefaultValue(true);
        builder.Property(a => a.IsHidden).HasDefaultValue(false);
        builder.Property(a => a.SortOrder).HasDefaultValue(0);
        builder.Property(a => a.Tier).HasDefaultValue(0);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(a => a.UpdatedAt).HasDefaultValueSql("now()");
    }
}
