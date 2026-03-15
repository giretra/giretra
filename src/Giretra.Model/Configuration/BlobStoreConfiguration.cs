using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Giretra.Model.Configuration;

public class BlobStoreConfiguration : IEntityTypeConfiguration<BlobStore>
{
    public void Configure(EntityTypeBuilder<BlobStore> builder)
    {
        builder.Property(b => b.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(b => b.UpdatedAt).HasDefaultValueSql("now()");
    }
}
