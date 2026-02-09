using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Giretra.Model.Configuration;

public class DealActionConfiguration : IEntityTypeConfiguration<DealAction>
{
    private static readonly ValueConverter<ActionType, string> ActionTypeConverter = new(
        v => ConvertToString(v),
        v => ConvertFromString(v));

    public void Configure(EntityTypeBuilder<DealAction> builder)
    {
        builder.Property(da => da.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(da => da.ActionType)
            .HasMaxLength(20)
            .HasConversion(ActionTypeConverter);

        builder.HasOne(da => da.Deal)
            .WithMany(d => d.DealActions)
            .HasForeignKey(da => da.DealId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static string ConvertToString(ActionType v) => v switch
    {
        ActionType.Cut => "cut",
        ActionType.Announce => "announce",
        ActionType.Accept => "accept",
        ActionType.Double => "double",
        ActionType.Redouble => "redouble",
        ActionType.PlayCard => "play_card",
        _ => v.ToString().ToLowerInvariant()
    };

    private static ActionType ConvertFromString(string v) => v switch
    {
        "cut" => ActionType.Cut,
        "announce" => ActionType.Announce,
        "accept" => ActionType.Accept,
        "double" => ActionType.Double,
        "redouble" => ActionType.Redouble,
        "play_card" => ActionType.PlayCard,
        _ => Enum.Parse<ActionType>(v, ignoreCase: true)
    };
}
