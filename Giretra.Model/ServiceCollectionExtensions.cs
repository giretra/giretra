using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Giretra.Model;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGiretraDb(this IServiceCollection services)
    {
        var connectionString = Environment.GetEnvironmentVariable("GIRETRA_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "The GIRETRA_CONNECTION_STRING environment variable is not set.");

        return services.AddGiretraDb(connectionString);
    }

    public static IServiceCollection AddGiretraDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GiretraDbContext>(options =>
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
