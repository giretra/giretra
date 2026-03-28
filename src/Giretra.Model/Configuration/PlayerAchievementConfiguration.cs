using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class PlayerAchievementConfiguration : IEntityTypeConfiguration<PlayerAchievement>
{
    public void Configure(EntityTypeBuilder<PlayerAchievement> builder)
    {
        builder.Property(pa => pa.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(pa => pa.EarnedAt).HasDefaultValueSql("now()");

        builder.HasOne(pa => pa.Player)
            .WithMany()
            .HasForeignKey(pa => pa.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pa => pa.Achievement)
            .WithMany(a => a.PlayerAchievements)
            .HasForeignKey(pa => pa.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pa => pa.Match)
            .WithMany()
            .HasForeignKey(pa => pa.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
