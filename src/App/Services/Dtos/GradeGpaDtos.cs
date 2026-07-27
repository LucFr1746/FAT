using Domain.Enums;

namespace Services.Dtos;

/// <summary>One assessment and the student's current score for it.</summary>
public sealed record GradeAssessmentDto(
    int AssessmentId,
    string Name,
    decimal Weight,
    decimal? MinScoreToPass,
    int DisplayOrder,
    decimal? Score)
{
    public const decimal MaximumScore = 10m;

    public decimal WeightPercent => Math.Round(Weight * 100m, 2);
    public bool HasScore => Score.HasValue;
}

/// <summary>
/// A course attempt prepared for the View Grades screen.
/// All values are projections; no calculated value is persisted.
/// </summary>
public sealed record GradeCourseDto(
    int EnrollmentId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    int SemesterDisplayOrder,
    decimal? FinalScore,
    string? LetterGrade,
    decimal? GradePoint,
    EnrollmentStatus Status,
    int AttemptNo,
    bool IsCounted,
    IReadOnlyList<GradeAssessmentDto> Assessments)
{
    public bool HasAnyScore => Assessments.Any(assessment => assessment.HasScore);

    public string StatusLabel => Status switch
    {
        EnrollmentStatus.Passed => "Passed",
        EnrollmentStatus.Failed => "Failed",
        EnrollmentStatus.Withdrawn => "Withdrawn",
        EnrollmentStatus.Studying when HasAnyScore => "Studying",
        _ => "Not Graded"
    };
}

/// <summary>An option in the semester filter on View Grades.</summary>
public sealed record GradeSemesterOptionDto(int? SemesterId, string DisplayName)
{
    public static GradeSemesterOptionDto All { get; } = new(null, "All semesters");
}

/// <summary>A student option for the administrator's grade-entry workspace.</summary>
public sealed record GradeStudentOptionDto(int StudentId, string StudentCode, string FullName)
{
    public string DisplayName => $"{StudentCode} — {FullName}";
}

/// <summary>A course attempt selectable in the administrator's grade-entry workspace.</summary>
public sealed record GradeEnrollmentOptionDto(
    int EnrollmentId,
    int StudentId,
    int CourseId,
    string CourseCode,
    string CourseName,
    string SemesterCode,
    decimal? FinalScore,
    EnrollmentStatus Status)
{
    public string DisplayName => $"{SemesterCode} · {CourseCode} — {CourseName}";
    public string FinalScoreDisplay => FinalScore.HasValue ? $"{FinalScore:N1}" : "Not graded";
}

/// <summary>One status segment in the academic-result distribution.</summary>
public sealed record StatusDistributionDto(string Status, int Count, decimal Percent);

/// <summary>
/// Complete application-layer result for the Statistics screen.
/// None of these aggregate values are stored in the database.
/// </summary>
public sealed record AcademicStatisticsDto(
    decimal? CumulativeGpa,
    IReadOnlyList<GpaTrendPointDto> GpaBySemester,
    int TotalCourses,
    int PassedCourses,
    int FailedCourses,
    int StudyingCourses,
    int NotGradedCourses,
    int TotalCredits,
    int GpaCredits,
    int CompletedCredits,
    int FailedCredits,
    int IncompleteCredits,
    int RequiredCredits,
    decimal ProgramProgressPercent,
    decimal? AverageFinalScore,
    CourseHighlightDto? HighestCourse,
    CourseHighlightDto? LowestCourse,
    IReadOnlyList<StatusDistributionDto> StatusDistribution);
