using FAT.App.Navigation;
using FAT.App.ViewModels.GradeGpa;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.Startup;

public static class GradeGpaRegistration
{
    public static IServiceCollection AddGradeGpaModule(this IServiceCollection services)
    {
        services.AddTransient<GradeListViewModel>();
        services.AddTransient<GradeEntryViewModel>();
        services.AddTransient<GpaCalculatorViewModel>();
        services.AddTransient<TranscriptViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddSingleton(new NavigationItem("Grades", PackIconKind.Grade, typeof(GradeListViewModel), 40));
        services.AddSingleton(new NavigationItem("Manage grades", PackIconKind.Pencil, typeof(GradeEntryViewModel), 41, true, "Administration"));
        services.AddSingleton(new NavigationItem("GPA calculator", PackIconKind.Calculator, typeof(GpaCalculatorViewModel), 42));
        services.AddSingleton(new NavigationItem("Transcript", PackIconKind.FileDocument, typeof(TranscriptViewModel), 43));
        services.AddSingleton(new NavigationItem("Statistics", PackIconKind.ChartLine, typeof(StatisticsViewModel), 44));
        return services;
    }
}
