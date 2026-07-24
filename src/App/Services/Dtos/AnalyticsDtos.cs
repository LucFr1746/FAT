using Domain.Enums;

namespace Services.Dtos;

/// <summary>One bar of the letter-grade distribution chart.</summary>
public sealed record GradeDistributionDto(string LetterGrade, int Count, decimal Percent);

/// <summary>One point on the GPA-by-semester chart.</summary>
public sealed record GpaTrendPointDto(string SemesterCode, int DisplayOrder, decimal? Gpa, int Credits);

/// <summary>A notable course (strongest or weakest).</summary>
public sealed record CourseHighlightDto(string CourseCode, string CourseName, decimal FinalScore, string? LetterGrade);

/// <summary>An academic warning.</summary>
public sealed record AcademicWarningDto(string SemesterCode, string Reason, decimal? SemesterGpa, int FailedCourses);

/// <summary>
/// Everything the dashboard needs, fetched in a SINGLE call.
///
/// Bundled deliberately rather than letting the dashboard call six services:
/// six sequential round trips make the main screen visibly stutter every time
/// it opens.
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
