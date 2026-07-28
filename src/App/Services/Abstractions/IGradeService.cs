using Domain.Entities;
using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Course registration, grade entry and final-score settlement.
/// FROZEN CONTRACT - owner: Member 4.
/// Backs View Grades, Manage Grades and Transcript.
/// </summary>
public interface IGradeService
{
    Task<TranscriptDto> GetTranscriptAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All course attempts and their assessment slots for the grade list and
    /// grade-entry screens.
    /// </summary>
    Task<IReadOnlyList<GradeCourseDto>> GetStudentGradesAsync(
        int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active programme terms used by the signed-in student's curriculum.
    /// Names and availability come from the existing Term table.
    /// </summary>
    Task<IReadOnlyList<GradeTermOptionDto>> GetTermOptionsAsync(
        int studentId, CancellationToken cancellationToken = default);

    /// <summary>All real calendar semesters available for a new enrollment.</summary>
    Task<IReadOnlyList<GradeSemesterOptionDto>> GetSemesterOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>The components and current scores of one course attempt.</summary>
    Task<IReadOnlyList<Grade>> GetGradesAsync(int enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a component score (insert or update) and then AUTOMATICALLY
    /// recomputes the final score for that enrollment.
    ///
    /// The two steps are fused on purpose: split them apart and it takes one
    /// forgotten recalculation for the transcript and the dashboard to show two
    /// different numbers.
    /// </summary>
    Task UpsertGradeAsync(int enrollmentId, int assessmentId, decimal score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a score for either an existing attempt or a curriculum placeholder.
    /// For a placeholder, a real Enrollment is created first using the existing
    /// prerequisite rules. Returns the real EnrollmentId.
    /// </summary>
    Task<int> UpsertStudentGradeAsync(
        int studentId,
        int enrollmentId,
        int courseId,
        int semesterId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Simplified grade-entry overload. A curriculum course without an
    /// enrollment is registered in the existing current calendar semester.
    /// </summary>
    Task<int> UpsertStudentGradeAsync(
        int studentId,
        int enrollmentId,
        int courseId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one recorded component score and returns the enrollment to an
    /// unfinished state until every component has a score again.
    /// </summary>
    Task DeleteGradeAsync(
        int enrollmentId, int assessmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes FinalScore, LetterGrade, GradePoint and Status for one attempt.
    ///
    /// It MUST produce exactly the same result as the settlement query in
    /// db/03_seed_demo.sql:
    ///   FinalScore = ROUND(SUM(Score * Weight), 1)
    ///   Passed     = FinalScore >= 5.0 AND no component below its MinScoreToPass
    /// </summary>
    Task RecalculateFinalScoreAsync(int enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a course, checking prerequisites and rejecting duplicates
    /// first. Returns the new EnrollmentId.
    /// </summary>
    Task<int> EnrollAsync(int studentId, int courseId, int semesterId, CancellationToken cancellationToken = default);

    Task WithdrawAsync(int enrollmentId, CancellationToken cancellationToken = default);
}
