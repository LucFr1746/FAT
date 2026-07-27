using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Dữ liệu cho Dashboard và màn hình Statistics.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 4.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>Toàn bộ dữ liệu Dashboard trong một lần gọi.</summary>
    Task<DashboardDto> GetDashboardAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeDistributionDto>> GetGradeDistributionAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GpaTrendPointDto>> GetGpaTrendAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Các môn điểm cao nhất.</summary>
    Task<IReadOnlyList<CourseHighlightDto>> GetTopCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default);

    /// <summary>Các môn điểm thấp nhất.</summary>
    Task<IReadOnlyList<CourseHighlightDto>> GetWeakestCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cảnh báo học vụ theo ngưỡng trong AcademicRules
    /// (GPA kỳ dưới 5.0, hoặc trượt từ 2 môn trở lên trong một kỳ).
    /// </summary>
    Task<IReadOnlyList<AcademicWarningDto>> GetAcademicWarningsAsync(int studentId, CancellationToken cancellationToken = default);
}
