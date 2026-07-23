using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// CRUD over a subject's assessment timeline (week, checkpoint, expected date).
///
/// FLM publishes session numbers but no dates, so an imported schedule always
/// arrives with ExpectedDate null and an administrator fills the dates in once
/// the semester calendar is fixed.
/// </summary>
public interface IAssessmentScheduleService
{
    /// <summary>A subject's timeline, in session order.</summary>
    Task<IReadOnlyList<AssessmentScheduleDto>> GetByCourseAsync(
        int courseId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(AssessmentScheduleDto schedule, CancellationToken cancellationToken = default);

    Task UpdateAsync(AssessmentScheduleDto schedule, CancellationToken cancellationToken = default);

    Task DeleteAsync(int assessmentScheduleId, CancellationToken cancellationToken = default);
}
