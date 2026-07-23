using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Data behind the dashboard and the Statistics screen.
/// FROZEN CONTRACT - owner: Member 4.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Everything the dashboard needs, in one call.</summary>
    Task<DashboardDto> GetDashboardAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeDistributionDto>> GetGradeDistributionAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GpaTrendPointDto>> GetGpaTrendAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Highest scoring courses.</summary>
    Task<IReadOnlyList<CourseHighlightDto>> GetTopCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default);

    /// <summary>Lowest scoring courses.</summary>
    Task<IReadOnlyList<CourseHighlightDto>> GetWeakestCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Academic warnings, using the thresholds in AcademicRules
    /// (semester GPA below 5.0, or two or more failed courses in one semester).
    /// </summary>
    Task<IReadOnlyList<AcademicWarningDto>> GetAcademicWarningsAsync(int studentId, CancellationToken cancellationToken = default);
}
