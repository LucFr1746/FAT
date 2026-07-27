using App.ViewModels.Grades;
using Microsoft.Extensions.DependencyInjection;
using Services.Abstractions;
using Services.Implementations;

namespace App.Startup;

/// <summary>Registers the Grade, GPA, Transcript and Statistics module.</summary>
public static class GradeModuleRegistration
{
    public static IServiceCollection AddGradeModule(this IServiceCollection services)
    {
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        services.AddTransient<GradeListViewModel>();
        services.AddTransient<GradeEntryViewModel>();
        services.AddTransient<GpaCalculatorViewModel>();
        services.AddTransient<TranscriptViewModel>();
        services.AddTransient<StatisticsViewModel>();

        return services;
    }
}
