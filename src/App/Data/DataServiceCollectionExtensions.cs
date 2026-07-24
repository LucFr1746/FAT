using Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Data;

/// <summary>Registers the DbContext and repositories with the DI container.</summary>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddAppData(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string is empty. Check ConnectionStrings:AppDatabase in appsettings.json.",
                nameof(connectionString));
        }

        // Scoped, NOT singleton: DbContext is not safe for concurrent use across
        // threads. Each navigation opens its own scope, so every view model gets
        // its own context.
        services.AddDbContext<FAT_DBContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Retry through brief connection blips - common right after the
                // machine boots and SQL Server is still coming up.
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(3), errorNumbersToAdd: null);
            });
        }, ServiceLifetime.Scoped);

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
