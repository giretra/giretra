using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Giretra.Model;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GiretraDbContext>
{
    public GiretraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GIRETRA_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "The GIRETRA_CONNECTION_STRING environment variable is not set. " +
                "Set it before running EF Core commands, e.g.: " +
                "export GIRETRA_CONNECTION_STRING=\"Host=localhost;Database=giretra;Username=postgres;Password=secret\"");

        var optionsBuilder = new DbContextOptionsBuilder<GiretraDbContext>();
        optionsBuilder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new GiretraDbContext(optionsBuilder.Options);
    }
}
