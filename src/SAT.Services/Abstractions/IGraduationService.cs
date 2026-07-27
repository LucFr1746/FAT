using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Curriculum Progress: đối chiếu khung chương trình với môn đã đạt.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 3.
/// </summary>
public interface IGraduationService
{
    Task<GraduationProgressDto> GetProgressAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Các môn trong khung chương trình mà sinh viên chưa đạt.
    /// Mỗi môn kèm cờ cho biết đã đủ điều kiện tiên quyết để đăng ký ngay chưa.
    /// </summary>
    Task<IReadOnlyList<MissingCourseDto>> GetMissingCoursesAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Các môn có thể đăng ký ở kỳ tới: nằm trong khung, chưa đạt, và đã đủ
    /// điều kiện tiên quyết. Đây là nguồn dữ liệu cho màn Academic Planner.
    /// </summary>
    Task<IReadOnlyList<MissingCourseDto>> GetEligibleCoursesAsync(int studentId, CancellationToken cancellationToken = default);
}
