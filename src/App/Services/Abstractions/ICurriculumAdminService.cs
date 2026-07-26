using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Curriculum administration: which subjects a programme teaches, in which kỳ,
/// in which order. Covers both "Assign Subject to Major" and
/// "Curriculum Management".
///
/// EVERY method that changes the set of subjects MUST resynchronise
/// Major.RequiredCredits in the same transaction. That column is the denominator
/// of the graduation percentage, so drift there is wrong for every student in
/// the programme and invisible in the UI.
/// </summary>
public interface ICurriculumAdminService
{
    /// <summary>A programme's curriculum, ordered by kỳ then display order.</summary>
    Task<IReadOnlyList<CurriculumItemDto>> GetByMajorAsync(
        int majorId, int? termNo = null, CancellationToken cancellationToken = default);

    /// <summary>Everything scheduled in one kỳ, across all programmes.</summary>
    Task<IReadOnlyList<CurriculumItemDto>> GetByTermAsync(
        int termNo, CancellationToken cancellationToken = default);

    /// <summary>Search across the curriculum.</summary>
    Task<IReadOnlyList<CurriculumItemDto>> SearchAsync(
        CurriculumFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Subjects NOT yet in a programme - the left-hand list of the assign screen.</summary>
    Task<IReadOnlyList<CourseDto>> GetUnassignedCoursesAsync(
        int majorId, string? keyword = null, CancellationToken cancellationToken = default);

    /// <summary>Adds one subject to a programme. Returns the new CurriculumId.</summary>
    Task<int> AssignAsync(
        int majorId, int courseId, int termNo, bool isMandatory = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds several subjects at once. Already-assigned subjects are skipped and
    /// reported rather than treated as an error - selecting one by accident
    /// should not abandon the other nineteen.
    /// </summary>
    Task<BulkOperationResultDto> BulkAssignAsync(
        int majorId, IReadOnlyList<int> courseIds, int termNo, bool isMandatory = true,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(int curriculumId, CancellationToken cancellationToken = default);

    Task<BulkOperationResultDto> BulkRemoveAsync(
        IReadOnlyList<int> curriculumIds, CancellationToken cancellationToken = default);

    Task UpdateItemAsync(
        int curriculumId, int termNo, bool isMandatory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites the order of the subjects inside one kỳ.
    /// <paramref name="orderedCurriculumIds"/> must list every item in that kỳ:
    /// a partial list would leave the rest holding stale positions.
    /// </summary>
    Task ReorderAsync(
        int majorId, int termNo, IReadOnlyList<int> orderedCurriculumIds,
        CancellationToken cancellationToken = default);

    /// <summary>Renders a programme's curriculum as CSV, ready to save to disk.</summary>
    Task<string> ExportToCsvAsync(int majorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which majors teach each of the given subjects, for the subject list's
    /// "Ngành Áp Dụng" column. Batched by design: one query for a whole page of
    /// subjects instead of one round trip per row.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetMajorCodesByCourseIdsAsync(
        IReadOnlyList<int> courseIds, CancellationToken cancellationToken = default);
}
