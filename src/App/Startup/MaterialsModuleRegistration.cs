using App.ViewModels.Materials;
using Microsoft.Extensions.DependencyInjection;
using Services.Abstractions;
using Services.Implementations;

namespace App.Startup;

/// <summary>
/// Registers the materials library module (Member 5).
///
/// A file per module, as docs/TEAM.md prescribes: the composition root gains a
/// single line and nobody else has to edit App.xaml.cs to add these screens.
/// </summary>
public static class MaterialsModuleRegistration
{
    public static IServiceCollection AddMaterialsModule(this IServiceCollection services)
    {
        // Scoped, matching FAT_DBContext: each navigation opens its own scope, so
        // a screen never shares a change tracker with the one before it.
        services.AddScoped<IMaterialLibraryService, MaterialLibraryService>();
        services.AddScoped<IMaterialService, MaterialService>();

        // Transient: the screen starts from a clean state every time it opens.
        services.AddTransient<MaterialLibraryViewModel>();

        return services;
    }
}
