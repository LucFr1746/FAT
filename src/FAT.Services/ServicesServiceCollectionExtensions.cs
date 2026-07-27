using FAT.Services.Abstractions;
using FAT.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.Services;

/// <summary>
/// Registers the CORE services that every module shares.
///
/// Module-specific services are registered by their own owner in
/// FAT.App/Startup/&lt;Module&gt;Registration.cs. Splitting it this way keeps five
/// people out of the same file and out of each other's merges.
/// </summary>
public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddFatCoreServices(this IServiceCollection services)
    {
        // Singleton: the application has exactly one signed-in session.
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

        // Scoped, because it depends on FatDbContext (also scoped). Registering
        // it as a singleton would pin one DbContext open for the lifetime of the
        // application - leaking memory and serving stale cached data.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGpaService, GpaService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        return services;
    }
}
