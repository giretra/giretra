using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Role)
            .HasMaxLength(20)
            .HasDefaultValue(UserRole.Normal)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<UserRole>(v, ignoreCase: true));

        builder.Property(u => u.IsBanned).HasDefaultValue(false);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(u => u.Email).IsUnique().HasFilter("email IS NOT NULL");
    }
}
