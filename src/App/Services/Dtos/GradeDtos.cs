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

/// <summary>A programme-term filter used by both grade screens.</summary>
public sealed record GradeTermOptionDto(int? TermNo, string Display)
{
    public static GradeTermOptionDto All { get; } = new(null, "Tất cả học kỳ");
}

/// <summary>A real calendar semester available when registering a missing course.</summary>
public sealed record GradeSemesterOptionDto(
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    int DisplayOrder,
    bool IsCurrent)
{
    public string Display => IsCurrent
        ? $"{SemesterCode} — {SemesterName} (hiện tại)"
        : $"{SemesterCode} — {SemesterName}";
}

/// <summary>
/// One curriculum course or enrollment attempt with all existing assessments.
/// EnrollmentId = 0 represents a curriculum course that has not been registered
/// yet; it exists only in the application layer until the first score is saved.
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
    IReadOnlyList<GradeAssessmentDto> Assessments,
    int CurriculumTermNo = -1,
    string CurriculumTermName = "",
    int CurriculumDisplayOrder = 0)
{
    public bool IsEnrolled => EnrollmentId > 0;
    public bool HasAnyGrade => Assessments.Any(a => a.HasScore);
    public bool IsFullyGraded => Assessments.Count > 0 && Assessments.All(a => a.HasScore);
    public bool CanManageGrades => !IsEnrolled || Status != EnrollmentStatus.Withdrawn;
    public string StatusDisplay => !IsEnrolled
        ? "Chưa có điểm"
        : Status switch
        {
            EnrollmentStatus.Passed => "Đạt",
            EnrollmentStatus.Failed => "Chưa đạt",
            EnrollmentStatus.Withdrawn => "Đã rút",
            _ when !HasAnyGrade => "Chưa có điểm",
            _ => "Đang học"
        };

    public string FinalScoreDisplay => FinalScore?.ToString("0.0") ?? "-";
    public string GradePointDisplay => GradePoint?.ToString("0.00") ?? "-";
    public string CurriculumTermDisplay => !string.IsNullOrWhiteSpace(CurriculumTermName)
        ? CurriculumTermName
        : CurriculumTermNo >= 0
            ? $"Kỳ {CurriculumTermNo}"
            : "Ngoài chương trình";
    public string TermAndSemesterDisplay => string.IsNullOrWhiteSpace(SemesterCode)
        ? CurriculumTermDisplay
        : $"{CurriculumTermDisplay} • {SemesterCode}";
    public string AttemptDisplay => IsEnrolled
        ? $"Lần học {Math.Max(1, AttemptNo)}"
        : "Chưa đăng ký";
}
