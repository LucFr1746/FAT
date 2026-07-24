using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Prerequisite checking. FROZEN CONTRACT - owner: Member 3.
/// Backs the Subject Detail and Curriculum Progress features.
/// </summary>
public interface IPrerequisiteService
{
    /// <summary>Direct prerequisites only (one level).</summary>
    Task<IReadOnlyList<CourseDto>> GetDirectPrerequisitesAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full prerequisite tree, resolved recursively.
    ///
    /// THE IMPLEMENTATION MUST GUARD AGAINST CYCLES with a HashSet of visited
    /// courses. Bad data (A requires B, B requires A) without that guard means
    /// infinite recursion and a hung application - during the demo. The database
    /// constraint only blocks cycles of length one, not longer ones.
    /// </summary>
    Task<PrerequisiteNodeDto> GetPrerequisiteTreeAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a student may take this course yet.
    /// Always returns the missing courses too, so the UI can explain the answer.
    /// </summary>
    Task<PrerequisiteCheckResult> CanEnrollAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    /// <summary>Batch check - used to filter the list of available courses.</summary>
    Task<IReadOnlyDictionary<int, PrerequisiteCheckResult>> CanEnrollManyAsync(
        int studentId, IEnumerable<int> courseIds, CancellationToken cancellationToken = default);
}
