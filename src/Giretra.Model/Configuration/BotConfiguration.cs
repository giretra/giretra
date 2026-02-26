using Giretra.Core.Players;
using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class BotConfiguration : IEntityTypeConfiguration<Bot>
{
    public void Configure(EntityTypeBuilder<Bot> builder)
    {
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(b => b.AgentTypeFactory).HasDefaultValue("");
        builder.Property(b => b.AuthorGithubUrl).HasMaxLength(512);
        builder.Property(b => b.BotType)
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<BotType>(v, ignoreCase: true))
            .HasDefaultValue(BotType.Deterministic);
        builder.Property(b => b.Difficulty).HasDefaultValue((short)1);
        builder.Property(b => b.Rating).HasDefaultValue(1000);
        builder.Property(b => b.IsActive).HasDefaultValue(true);
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
    }
}
