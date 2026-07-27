using Domain.Enums;

namespace Services.Dtos;

/// <summary>An assessment and the score currently recorded for one course attempt.</summary>
public sealed record GradeAssessmentDto(
    int AssessmentId,
    string Name,
    decimal Weight,
    decimal? MinScoreToPass,
    int DisplayOrder,
    int? GradeId,
    decimal? Score)
{
    /// <summary>The current schema stores every component on the 10-point scale.</summary>
    public const decimal MaxScore = 10m;

    public decimal WeightPercent => Math.Round(Weight * 100m, 2);
    public bool HasScore => Score.HasValue;
    public string ScoreDisplay => Score?.ToString("0.##") ?? "Chưa có";
    public string MinimumDisplay => MinScoreToPass?.ToString("0.##") ?? "-";
}

/// <summary>
/// One enrollment with all existing assessments, including components that have
/// not received a grade yet.
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
    bool SemesterIsCurrent,
    EnrollmentStatus Status,
    decimal? FinalScore,
    string? LetterGrade,
    decimal? GradePoint,
    bool CountsTowardGpa,
    bool IsCounted,
    int AttemptNo,
    IReadOnlyList<GradeAssessmentDto> Assessments)
{
    public bool HasAnyGrade => Assessments.Any(a => a.HasScore);
    public bool IsFullyGraded => Assessments.Count > 0 && Assessments.All(a => a.HasScore);
    public bool CanManageGrades => Status != EnrollmentStatus.Withdrawn;

    public string StatusDisplay => Status switch
    {
        EnrollmentStatus.Passed => "Passed",
        EnrollmentStatus.Failed => "Failed",
        EnrollmentStatus.Withdrawn => "Withdrawn",
        _ when !HasAnyGrade => "Not Graded",
        _ => "Studying"
    };

    public string FinalScoreDisplay => FinalScore?.ToString("0.0") ?? "-";
    public string GradePointDisplay => GradePoint?.ToString("0.00") ?? "-";
}
