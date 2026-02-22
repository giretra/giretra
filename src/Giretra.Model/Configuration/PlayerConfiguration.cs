using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.PlayerType)
            .HasMaxLength(10)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<PlayerType>(v, ignoreCase: true));

        builder.Property(p => p.EloRating).HasDefaultValue(1000);
        builder.Property(p => p.EloIsPublic).HasDefaultValue(true);
        builder.Property(p => p.GamesPlayed).HasDefaultValue(0);
        builder.Property(p => p.GamesWon).HasDefaultValue(0);
        builder.Property(p => p.WinStreak).HasDefaultValue(0);
        builder.Property(p => p.BestWinStreak).HasDefaultValue(0);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(p => p.User)
            .WithOne(u => u.Player)
            .HasForeignKey<Player>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Bot)
            .WithOne(b => b.Player)
            .HasForeignKey<Player>(p => p.BotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_player_type_ref",
            "(player_type = 'human' AND user_id IS NOT NULL AND bot_id IS NULL) OR (player_type = 'bot' AND bot_id IS NOT NULL AND user_id IS NULL)"));

        // Filtered unique indexes
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL");

        builder.HasIndex(p => p.BotId)
            .IsUnique()
            .HasFilter("bot_id IS NOT NULL");

        builder.HasIndex(p => p.EloRating)
            .IsDescending()
            .HasFilter("elo_is_public = TRUE");
    }
}
