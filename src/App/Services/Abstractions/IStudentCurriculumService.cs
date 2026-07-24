using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// The student's own view of their programme, and the retake flow.
///
/// The workflow this backs:
///   Register -&gt; Login -&gt; pick Major -&gt; pick current Kỳ -&gt; see the subjects,
///   each with its credits, GPA flag, grade structure, materials, timeline and
///   prerequisites.
///
/// THE PREREQUISITE RULE: a subject whose prerequisites are unmet is REMOVED
/// from the list, not shown greyed out. That is the requirement as written, and
/// it is why <see cref="GetTermCurriculumAsync"/> also returns a count of what
/// it hid - a silently shorter list would otherwise look like missing data.
/// </summary>
public interface IStudentCurriculumService
{
    /// <summary>
    /// The subjects of one kỳ that the student may take, with locked ones
    /// omitted and counted.
    /// </summary>
    Task<StudentTermCurriculumDto> GetTermCurriculumAsync(
        int studentId, int termNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full detail for one subject: grade structure, materials, timeline and the
    /// prerequisite check with its reasons.
    /// </summary>
    Task<StudentSubjectDetailDto?> GetSubjectDetailAsync(
        int studentId, int courseId, CancellationToken cancellationToken = default);

    /// <summary>Sets the student's programme. Clears the current kỳ, which no longer applies.</summary>
    Task SetMajorAsync(int studentId, int majorId, CancellationToken cancellationToken = default);

    /// <summary>Sets the kỳ the curriculum screen opens on.</summary>
    Task SetCurrentTermAsync(int studentId, int termNo, CancellationToken cancellationToken = default);

    /// <summary>Subjects the student failed and has not since passed.</summary>
    Task<IReadOnlyList<RetakeCandidateDto>> GetRetakeCandidatesAsync(
        int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a failed subject again - the "Add Retake Subject" button.
    ///
    /// Creates NO new subject: only a new Enrollment, with AttemptNo one higher,
    /// while every earlier attempt is marked IsCounted = false so the GPA counts
    /// the subject once. Several different subjects may be retaken in the same
    /// semester.
    /// </summary>
    Task<int> AddRetakeAsync(
        int studentId, int courseId, int semesterId, CancellationToken cancellationToken = default);
}
