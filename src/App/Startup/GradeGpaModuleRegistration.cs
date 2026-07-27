using App.ViewModels.GradeGpa;
using Microsoft.Extensions.DependencyInjection;
using Services.Abstractions;
using Services.Implementations;

namespace App.Startup;

/// <summary>Registers the Grade, GPA, Transcript and Statistics module.</summary>
public static class GradeGpaModuleRegistration
{
    public static IServiceCollection AddGradeGpaModule(this IServiceCollection services)
    {
        services.AddScoped<GradeService>();
        services.AddScoped<IGradeService>(
            provider => provider.GetRequiredService<GradeService>());
        services.AddScoped<IGradeWorkspaceService, GradeWorkspaceService>();

        services.AddScoped<AnalyticsService>();
        services.AddScoped<IAnalyticsService>(
            provider => provider.GetRequiredService<AnalyticsService>());
        services.AddScoped<IStatisticsService>(
            provider => provider.GetRequiredService<AnalyticsService>());

        services.AddTransient<GradeListViewModel>();
        services.AddTransient<GradeEntryViewModel>();
        services.AddTransient<GpaCalculatorViewModel>();
        services.AddTransient<TranscriptViewModel>();
        services.AddTransient<StatisticsViewModel>();

        return services;
    }
}
