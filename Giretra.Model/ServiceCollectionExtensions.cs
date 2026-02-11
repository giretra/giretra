using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Giretra.Model;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGiretraDb(this IServiceCollection services)
    {
        return services.AddGiretraDb(ConnectionStringBuilder.FromEnvironment());
    }

    public static IServiceCollection AddGiretraDb(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GiretraDbContext>(options =>
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        return services;
    }
}
