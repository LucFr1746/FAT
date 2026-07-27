using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Read models and the delete operation used by View Grades and Manage Grades.
/// Grade inserts and updates remain on <see cref="IGradeService"/>.
/// </summary>
public interface IGradeWorkspaceService
{
    Task<IReadOnlyList<GradeCourseDto>> GetStudentGradesAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeStudentOptionDto>> GetStudentsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeEnrollmentOptionDto>> GetEnrollmentsAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeAssessmentDto>> GetAssessmentScoresAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default);

    Task DeleteGradeAsync(
        int enrollmentId,
        int assessmentId,
        CancellationToken cancellationToken = default);
}
