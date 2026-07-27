using SAT.Domain.Enums;

namespace SAT.Services.Dtos;

/// <summary>Một cột trong biểu đồ phân bố điểm chữ.</summary>
public sealed record GradeDistributionDto(string LetterGrade, int Count, decimal Percent);

/// <summary>Một điểm trên biểu đồ GPA theo kỳ.</summary>
public sealed record GpaTrendPointDto(string SemesterCode, int DisplayOrder, decimal? Gpa, int Credits);

/// <summary>Một môn nổi bật (mạnh nhất hoặc yếu nhất).</summary>
public sealed record CourseHighlightDto(string CourseCode, string CourseName, decimal FinalScore, string? LetterGrade);

/// <summary>Cảnh báo học vụ.</summary>
public sealed record AcademicWarningDto(string SemesterCode, string Reason, decimal? SemesterGpa, int FailedCourses);

/// <summary>
/// Toàn bộ dữ liệu Dashboard, lấy trong MỘT lần gọi.
///
/// Gói chung thay vì để Dashboard gọi 6 service riêng lẻ: 6 lần round-trip
/// nối tiếp sẽ làm màn hình chính giật thấy rõ mỗi lần mở.
/// </summary>
public sealed record DashboardDto(
    string StudentCode,
    string FullName,
    string MajorName,
    decimal? CumulativeGpa,
    DegreeClassification Classification,
    string ClassificationName,
    int EarnedCredits,
    int RequiredCredits,
    int InProgressCredits,
    decimal GraduationPercent,
    int PassedCourses,
    int FailedCourses,
    int StudyingCourses,
    string? CurrentSemesterCode,
    IReadOnlyList<GpaTrendPointDto> GpaTrend,
    IReadOnlyList<GradeDistributionDto> GradeDistribution,
    IReadOnlyList<TranscriptItemDto> CurrentCourses,
    IReadOnlyList<AcademicWarningDto> Warnings);
