using Domain.Enums;

namespace Services.Dtos;

/// <summary>
/// A subject as the student sees it on their curriculum screen.
/// </summary>
public sealed record StudentSubjectDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    bool CountsTowardGpa,
    int TermNo,
    bool IsMandatory,
    string? Description,
    string? PrerequisiteText,
    IReadOnlyList<string> PrerequisiteCodes,
    EnrollmentStatus? MyStatus,
    decimal? MyFinalScore,
    int MyAttemptCount,
    int MaterialCount,
    int AssessmentCount,
    bool IsEligible = true)
{
    public bool IsPassed => MyStatus == EnrollmentStatus.Passed;
    public bool IsFailed => MyStatus == EnrollmentStatus.Failed;
    public bool IsStudying => MyStatus == EnrollmentStatus.Studying;
    public bool IsIneligible => !IsEligible;
    public bool IsUncompleted => !IsPassed && !IsFailed && !IsStudying && IsEligible;
    public bool IsRetake => MyAttemptCount > 1;

    /// <summary>"Có"/"Không", matching the wording FLM uses.</summary>
    public string GpaDisplay => CountsTowardGpa ? "Có" : "Không";

    /// <summary>Formatted score string e.g. "8.5" or "Chưa có điểm".</summary>
    public string FinalScoreDisplay => MyFinalScore.HasValue ? MyFinalScore.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "Chưa có điểm";
}


/// <summary>Summary of curriculum progress for a student.</summary>
public sealed record CurriculumProgressDto(
    int CompletedSubjects,
    int TotalSubjects,
    int CompletedCredits,
    int TotalCredits,
    double ProgressPercentage);

/// <summary>
/// The subjects of one kỳ that a student may actually take.
///
/// <see cref="HiddenByPrerequisiteCount"/> exists because the rule is to HIDE a
/// locked subject rather than grey it out. Reporting the count keeps that from
/// looking like data loss: the screen can say "3 subjects are hidden until you
/// pass their prerequisites" instead of leaving a silent gap in the list.
/// </summary>
public sealed record StudentTermCurriculumDto(
    int MajorId,
    string MajorCode,
    string MajorName,
    int TermNo,
    string TermName,
    IReadOnlyList<StudentSubjectDto> Subjects,
    int HiddenByPrerequisiteCount,
    IReadOnlyList<string> HiddenSubjectCodes,
    CurriculumProgressDto Progress)
{
    public int TermCredits => Subjects.Sum(s => s.Credits);
    public bool HasHiddenSubjects => HiddenByPrerequisiteCount > 0;
}

/// <summary>Everything the subject detail screen shows.</summary>
public sealed record StudentSubjectDetailDto(
    StudentSubjectDto Subject,
    IReadOnlyList<AssessmentDto> GradeStructure,
    IReadOnlyList<SubjectMaterialDto> Materials,
    IReadOnlyList<AssessmentScheduleDto> Schedule,
    PrerequisiteCheckResult PrerequisiteCheck,
    GradeStructureValidationDto GradeStructureValidation);

/// <summary>A subject the student failed and may register again.</summary>
public sealed record RetakeCandidateDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    decimal? LastScore,
    int AttemptCount,
    string LastSemesterCode);

/// <summary>Outcome of a GPA forecast.</summary>
public sealed record GpaPredictionDto(
    decimal? CurrentGpa,
    decimal PredictedGpa,
    int RetakenSubjectCount,
    DegreeClassification BaseClassification,
    string BaseClassificationName,
    DegreeClassification AdjustedClassification,
    string AdjustedClassificationName,
    int ProjectedEarnedCredits,
    int RequiredCredits,
    string? DemotionReason)
{
    /// <summary>True when retakes cost the student a classification rank.</summary>
    public bool IsDemoted => AdjustedClassification != BaseClassification;

    public decimal GpaDelta => PredictedGpa - (CurrentGpa ?? 0m);
}
