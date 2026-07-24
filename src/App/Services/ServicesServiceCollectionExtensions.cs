using Services.Abstractions;
using Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Services;

/// <summary>
/// Registers the CORE services that every module shares.
///
/// Module-specific services are registered by their own owner in
/// App/Startup/&lt;Module&gt;Registration.cs of module. Splitting it this way keeps five
/// people out of the same file and out of each other's merges.
/// </summary>
public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Singleton: the application has exactly one signed-in session.
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

        // Scoped services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // Google OAuth Service
        services.AddSingleton<IGoogleOAuthService, GoogleOAuthService>();

        return services;
    }
}
