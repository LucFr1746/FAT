using SAT.Domain.Enums;

namespace SAT.Services.Dtos;

/// <summary>Một dòng trong bảng điểm.</summary>
public sealed record TranscriptItemDto(
    int EnrollmentId,
    string CourseCode,
    string CourseName,
    int Credits,
    decimal? FinalScore,
    string? LetterGrade,
    decimal? GradePoint,
    EnrollmentStatus Status,
    bool IsCounted,
    int AttemptNo)
{
    /// <summary>Lần học này có bị loại khỏi GPA vì đã học lại không.</summary>
    public bool IsSupersededRetake => !IsCounted && Status != EnrollmentStatus.Studying;
}

/// <summary>Bảng điểm của một học kỳ.</summary>
public sealed record SemesterTranscriptDto(
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    int DisplayOrder,
    bool IsCurrent,
    IReadOnlyList<TranscriptItemDto> Items,
    decimal? SemesterGpa,
    int EarnedCredits);

/// <summary>Toàn bộ bảng điểm, gom theo kỳ và sắp theo thứ tự thời gian.</summary>
public sealed record TranscriptDto(
    int StudentId,
    string StudentCode,
    string FullName,
    IReadOnlyList<SemesterTranscriptDto> Semesters);

/// <summary>GPA của một kỳ - một điểm trên biểu đồ xu hướng.</summary>
public sealed record SemesterGpaDto(
    int SemesterId,
    string SemesterCode,
    int DisplayOrder,
    decimal? Gpa,
    int EarnedCredits);

/// <summary>Tổng hợp GPA của sinh viên.</summary>
public sealed record GpaSummaryDto(
    decimal? CumulativeGpa,
    DegreeClassification Classification,
    string ClassificationName,
    IReadOnlyList<SemesterGpaDto> BySemester);

/// <summary>Thống kê tín chỉ.</summary>
public sealed record CreditSummaryDto(
    int EarnedCredits,
    int InProgressCredits,
    int RequiredCredits)
{
    public int RemainingCredits => Math.Max(0, RequiredCredits - EarnedCredits);

    public decimal CompletionPercent => RequiredCredits <= 0
        ? 0m
        : Math.Round(100m * EarnedCredits / RequiredCredits, 1);
}
