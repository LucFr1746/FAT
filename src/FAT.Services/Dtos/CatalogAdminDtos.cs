using FAT.Domain.Constants;

namespace FAT.Services.Dtos;

/// <summary>A kỳ of the study path, as shown on the Manage Semester screen.</summary>
public sealed record TermDto(
    int TermId,
    int TermNo,
    string TermName,
    string? Description,
    bool IsActive,
    int SubjectCount = 0);

/// <summary>A syllabus reading or link.</summary>
public sealed record SubjectMaterialDto(
    int SubjectMaterialId,
    int CourseId,
    string Title,
    string? Description,
    string? Url,
    string? Author,
    string? Publisher,
    string? Isbn,
    int DisplayOrder,
    bool IsActive)
{
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}

/// <summary>One grade component of a subject.</summary>
public sealed record AssessmentDto(
    int AssessmentId,
    int CourseId,
    string Name,
    decimal Weight,
    decimal? MinScoreToPass,
    int DisplayOrder)
{
    /// <summary>The weight as a percentage, which is how the UI shows and edits it.</summary>
    public decimal WeightPercent => Math.Round(Weight * 100m, 2);
}

/// <summary>
/// Whether a subject's grade components add up.
///
/// Reported rather than thrown on read, because an unbalanced structure still
/// has to be displayed for the administrator to fix.
/// </summary>
public sealed record GradeStructureValidationDto(
    int CourseId,
    decimal TotalWeight,
    bool IsBalanced,
    IReadOnlyList<string> Errors)
{
    public decimal TotalWeightPercent => Math.Round(TotalWeight * 100m, 2);

    public static GradeStructureValidationDto FromWeights(int courseId, decimal totalWeight)
    {
        var isBalanced = CatalogRules.IsWeightTotalValid(totalWeight);

        return new GradeStructureValidationDto(
            courseId,
            totalWeight,
            isBalanced,
            isBalanced
                ? []
                : [$"Tổng trọng số các cột điểm là {Math.Round(totalWeight * 100m, 2)}%, phải bằng 100%."]);
    }
}

/// <summary>One planned checkpoint on a subject's timeline.</summary>
public sealed record AssessmentScheduleDto(
    int AssessmentScheduleId,
    int CourseId,
    int SessionNo,
    int? WeekNo,
    string Title,
    string? Description,
    DateTime? ExpectedDate,
    string? TeachingType,
    int? AssessmentId,
    string? AssessmentName);

/// <summary>
/// One stored prerequisite edge.
///
/// Rows sharing a <see cref="GroupNo"/> above zero are ALTERNATIVES - any one of
/// them satisfies the requirement. GroupNo 0 means the subject is required
/// outright.
/// </summary>
public sealed record PrerequisiteEdgeDto(
    int PrerequisiteId,
    int CourseId,
    int RequiredCourseId,
    string RequiredCourseCode,
    string RequiredCourseName,
    int GroupNo)
{
    public bool IsAlternative => GroupNo > 0;
}

/// <summary>Filter on the Manage Major screen.</summary>
public sealed record MajorFilter(
    string? Keyword = null,
    bool? IsActive = null);

/// <summary>Filter on the curriculum screens.</summary>
public sealed record CurriculumFilter(
    int? MajorId = null,
    int? TermNo = null,
    string? Keyword = null,
    bool? IsMandatory = null);

/// <summary>
/// Outcome of a bulk assign or remove.
///
/// Reports what was skipped and why: a bulk operation that silently drops half
/// its input looks like it worked and is discovered much later.
/// </summary>
public sealed record BulkOperationResultDto(
    int Succeeded,
    int Skipped,
    IReadOnlyList<string> Messages)
{
    public bool HasSkipped => Skipped > 0;
}
