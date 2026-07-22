using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Curriculum Progress: the curriculum measured against what has been passed.
/// FROZEN CONTRACT - owner: Member 3.
/// </summary>
public interface IGraduationService
{
    Task<GraduationProgressDto> GetProgressAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Curriculum courses the student has not passed yet. Each one carries a
    /// flag saying whether its prerequisites are already satisfied.
    /// </summary>
    Task<IReadOnlyList<MissingCourseDto>> GetMissingCoursesAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Courses that can be taken next term: in the curriculum, not yet passed,
    /// and with all prerequisites met.
    /// </summary>
    Task<IReadOnlyList<MissingCourseDto>> GetEligibleCoursesAsync(int studentId, CancellationToken cancellationToken = default);
}
