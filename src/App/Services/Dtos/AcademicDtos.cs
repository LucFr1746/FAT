using Domain.Enums;

namespace Services.Dtos;

/// <summary>A single row of the transcript.</summary>
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
    /// <summary>True when this attempt is excluded from the GPA by a later retake.</summary>
    public bool IsSupersededRetake => !IsCounted && Status != EnrollmentStatus.Studying;

    public string StatusDisplay => Status.ToString();
    public string FinalScoreDisplay => FinalScore?.ToString("0.0") ?? "-";
    public string GradePointDisplay => GradePoint?.ToString("0.00") ?? "-";
}

/// <summary>The transcript for a single semester.</summary>
public sealed record SemesterTranscriptDto(
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    int DisplayOrder,
    bool IsCurrent,
    IReadOnlyList<TranscriptItemDto> Items,
    decimal? SemesterGpa,
    int EarnedCredits);

/// <summary>The full transcript, grouped by semester in chronological order.</summary>
public sealed record TranscriptDto(
    int StudentId,
    string StudentCode,
    string FullName,
    IReadOnlyList<SemesterTranscriptDto> Semesters,
    string? MajorName = null);

/// <summary>GPA for one semester - a single point on the trend chart.</summary>
public sealed record SemesterGpaDto(
    int SemesterId,
    string SemesterCode,
    int DisplayOrder,
    decimal? Gpa,
    int EarnedCredits,
    int GpaCredits = 0);

/// <summary>Aggregated GPA information for a student.</summary>
public sealed record GpaSummaryDto(
    decimal? CumulativeGpa,
    DegreeClassification Classification,
    string ClassificationName,
    IReadOnlyList<SemesterGpaDto> BySemester);

/// <summary>Credit totals.</summary>
public sealed record CreditSummaryDto(
    int EarnedCredits,
    int InProgressCredits,
    int RequiredCredits,
    int FailedCredits = 0)
{
    public int RemainingCredits => Math.Max(0, RequiredCredits - EarnedCredits);

    public decimal CompletionPercent => RequiredCredits <= 0
        ? 0m
        : Math.Round(100m * EarnedCredits / RequiredCredits, 1);
}
