using FAT.Domain.Enums;

namespace FAT.Services.Dtos;

/// <summary>
/// Result of a prerequisite check.
/// Always carries the REASON, not just a boolean: blocking a student from
/// registering without telling them why makes the feature useless.
/// </summary>
public sealed record PrerequisiteCheckResult(
    bool CanEnroll,
    IReadOnlyList<UnmetPrerequisiteDto> Unmet)
{
    public static PrerequisiteCheckResult Ok() => new(true, []);

    public string BuildReason() => CanEnroll
        ? "All prerequisites satisfied."
        : "Missing prerequisites: " + string.Join(", ", Unmet.Select(u => u.CourseCode));
}

/// <summary>A prerequisite that has not been satisfied.</summary>
public sealed record UnmetPrerequisiteDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    PrerequisiteType Type,
    // Where the student currently stands on this course: never taken,
    // in progress, or failed.
    EnrollmentStatus? CurrentStatus);

/// <summary>A course still outstanding for graduation.</summary>
public sealed record MissingCourseDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int TermNo,
    // Whether the prerequisites are already met, so it can be taken next term.
    bool IsEligibleNow);

/// <summary>Graduation progress.</summary>
public sealed record GraduationProgressDto(
    int EarnedCredits,
    int RequiredCredits,
    int TotalCurriculumCourses,
    int CompletedCourses,
    decimal ProgressPercent,
    decimal? CurrentGpa,
    DegreeClassification ProjectedClassification,
    string ProjectedClassificationName,
    IReadOnlyList<MissingCourseDto> MissingCourses);

/// <summary>A hypothetical score for one course - input to the what-if feature.</summary>
public sealed record PlannedGradeDto(int CourseId, decimal ExpectedScore);

/// <summary>Result of a what-if GPA simulation.</summary>
public sealed record WhatIfResultDto(
    decimal? CurrentGpa,
    decimal ProjectedGpa,
    decimal Delta,
    DegreeClassification ProjectedClassification,
    string ProjectedClassificationName,
    int ProjectedEarnedCredits);

/// <summary>One row of a study plan.</summary>
public sealed record AcademicPlanItemDto(
    int PlanItemId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int? SemesterId,
    string? SemesterCode,
    int? TargetTermNo,
    decimal? ExpectedScore);

/// <summary>A study plan together with its rows.</summary>
public sealed record AcademicPlanDto(
    int PlanId,
    int StudentId,
    string PlanName,
    string? Note,
    IReadOnlyList<AcademicPlanItemDto> Items)
{
    public int TotalPlannedCredits => Items.Sum(i => i.Credits);
}

/// <summary>Result of validating a study plan.</summary>
public sealed record PlanValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static PlanValidationResult Valid() => new(true, [], []);
}
