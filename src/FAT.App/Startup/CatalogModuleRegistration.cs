using FAT.App.ViewModels.Catalog;
using FAT.App.ViewModels.Student;
using FAT.Services.Abstractions;
using FAT.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.Startup;

/// <summary>
/// Registers the catalog, curriculum and student-workflow module.
///
/// A file per module, as docs/TEAM.md prescribes: five people adding screens to
/// App.xaml.cs directly means five people editing the same twenty lines, which
/// is where the merge conflicts come from. App.xaml.cs calls this once.
/// </summary>
public static class CatalogModuleRegistration
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        // ----- Services -----
        // Scoped, matching FatDbContext: each navigation opens its own scope, so
        // a screen never shares a change tracker with the one before it.
        services.AddScoped<ICurriculumAdminService, CurriculumAdminService>();
        services.AddScoped<ICatalogAdminService, CatalogAdminService>();
        services.AddScoped<ITermService, TermService>();
        services.AddScoped<ISubjectMaterialService, SubjectMaterialService>();
        services.AddScoped<IGradeStructureService, GradeStructureService>();
        services.AddScoped<IAssessmentScheduleService, AssessmentScheduleService>();
        services.AddScoped<IFlmImportService, FlmImportService>();

        // Read side
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IPrerequisiteService, PrerequisiteService>();
        services.AddScoped<IGraduationService, GraduationService>();

        // GpaPredictionService needs the CONCRETE GpaService: it reuses that
        // class's internal row set so the forecast and the real GPA can never be
        // computed from different inputs. Registering the concrete type as well
        // keeps a single instance behind both.
        services.AddScoped<GpaService>();
        services.AddScoped<IGpaService>(sp => sp.GetRequiredService<GpaService>());

        services.AddScoped<IStudentCurriculumService, StudentCurriculumService>();
        services.AddScoped<IGpaPredictionService, GpaPredictionService>();

        // ----- Admin view models -----
        // Transient: a screen must start from a clean state every time it is
        // opened, not from whatever the last visit left behind.
        services.AddTransient<MajorAdminViewModel>();
        services.AddTransient<TermAdminViewModel>();
        services.AddTransient<SemesterAdminViewModel>();
        services.AddTransient<SubjectAdminViewModel>();
        services.AddTransient<CurriculumAdminViewModel>();
        services.AddTransient<FlmImportViewModel>();

        // ----- Student view models -----
        services.AddTransient<MyCurriculumViewModel>();
        services.AddTransient<GpaPredictionViewModel>();

        return services;
    }
}
