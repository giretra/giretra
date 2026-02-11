using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Giretra.Model;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GiretraDbContext>
{
    public GiretraDbContext CreateDbContext(string[] args)
    {
        var connectionString = ConnectionStringBuilder.FromEnvironment();

        var optionsBuilder = new DbContextOptionsBuilder<GiretraDbContext>();
        optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new GiretraDbContext(optionsBuilder.Options);
    }
}
