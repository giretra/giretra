using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class EloHistoryConfiguration : IEntityTypeConfiguration<EloHistory>
{
    public void Configure(EntityTypeBuilder<EloHistory> builder)
    {
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.RecordedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.InvolvedBots).HasDefaultValue(false);

        // Filtered index for efficient weekly bot-Elo cap queries
        builder.HasIndex(e => new { e.PlayerId, e.RecordedAt })
            .HasFilter("involved_bots = TRUE AND elo_change > 0")
            .HasDatabaseName("ix_elo_history_bot_gains");

        builder.HasOne(e => e.Player)
            .WithMany(p => p.EloHistories)
            .HasForeignKey(e => e.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Match)
            .WithMany(m => m.EloHistories)
            .HasForeignKey(e => e.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
