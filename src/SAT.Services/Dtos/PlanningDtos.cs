using SAT.Domain.Enums;

namespace SAT.Services.Dtos;

/// <summary>
/// Kết quả kiểm tra điều kiện tiên quyết.
/// Luôn kèm LÝ DO, không chỉ trả true/false: sinh viên bị chặn đăng ký mà
/// không biết vì sao thì tính năng này vô dụng.
/// </summary>
public sealed record PrerequisiteCheckResult(
    bool CanEnroll,
    IReadOnlyList<UnmetPrerequisiteDto> Unmet)
{
    public static PrerequisiteCheckResult Ok() => new(true, []);

    public string BuildReason() => CanEnroll
        ? "Đủ điều kiện đăng ký."
        : "Chưa đạt môn tiên quyết: " + string.Join(", ", Unmet.Select(u => u.CourseCode));
}

/// <summary>Một môn tiên quyết chưa đạt.</summary>
public sealed record UnmetPrerequisiteDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    PrerequisiteType Type,
    /* Trạng thái hiện tại của sinh viên với môn này: chưa học, đang học, hay đã trượt. */
    EnrollmentStatus? CurrentStatus);

/// <summary>Một môn còn thiếu để tốt nghiệp.</summary>
public sealed record MissingCourseDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int TermNo,
    /* Đã đủ điều kiện tiên quyết để đăng ký ngay kỳ tới chưa. */
    bool IsEligibleNow);

/// <summary>Tiến độ tốt nghiệp.</summary>
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

/// <summary>Điểm giả định cho một môn, đầu vào của What-if.</summary>
public sealed record PlannedGradeDto(int CourseId, decimal ExpectedScore);

/// <summary>Kết quả mô phỏng What-if GPA.</summary>
public sealed record WhatIfResultDto(
    decimal? CurrentGpa,
    decimal ProjectedGpa,
    decimal Delta,
    DegreeClassification ProjectedClassification,
    string ProjectedClassificationName,
    int ProjectedEarnedCredits);

/// <summary>Một dòng trong kế hoạch học tập.</summary>
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

/// <summary>Kế hoạch học tập kèm các dòng của nó.</summary>
public sealed record AcademicPlanDto(
    int PlanId,
    int StudentId,
    string PlanName,
    string? Note,
    IReadOnlyList<AcademicPlanItemDto> Items)
{
    public int TotalPlannedCredits => Items.Sum(i => i.Credits);
}

/// <summary>Kết quả kiểm tra tính hợp lệ của kế hoạch.</summary>
public sealed record PlanValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static PlanValidationResult Valid() => new(true, [], []);
}
