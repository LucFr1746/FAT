using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// CRUD over a subject's grade components (PT1, Assignment, Final exam, ...).
///
/// THE INVARIANT: the weights of one subject must total exactly 100%. Every
/// final score is SUM(Score * Weight), so a structure totalling 90% quietly caps
/// that subject at 9.0 and no error is ever raised. The service enforces the
/// total on write and reports it on read.
/// </summary>
public interface IGradeStructureService
{
    /// <summary>A subject's grade components, in display order.</summary>
    Task<IReadOnlyList<AssessmentDto>> GetByCourseAsync(
        int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a component.
    /// <paramref name="allowUnbalanced"/> lets a structure be built up one row at
    /// a time - without it the FIRST component would always be rejected for not
    /// already totalling 100%.
    /// </summary>
    Task<int> CreateAsync(
        AssessmentDto assessment, bool allowUnbalanced = true, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        AssessmentDto assessment, bool allowUnbalanced = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a component. REFUSES when scores have already been recorded
    /// against it, because deleting it would silently change the final score of
    /// every student who has one.
    /// </summary>
    Task DeleteAsync(int assessmentId, CancellationToken cancellationToken = default);

    /// <summary>Whether the weights add up, and what they add up to.</summary>
    Task<GradeStructureValidationDto> ValidateWeightsAsync(
        int courseId, CancellationToken cancellationToken = default);
}
