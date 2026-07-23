using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Catalog administration - the WRITE side. Admin accounts only.
/// FROZEN CONTRACT - owner: Member 2.
///
/// Covers all five features: Manage Major, Manage Semester, Manage Subject,
/// Assign Subject to Major, Curriculum Management.
/// </summary>
public interface ICatalogAdminService
{
    // ----- Manage Major -----

    /// <summary>Majors for the admin list, with search and status filtering.</summary>
    Task<IReadOnlyList<MajorDto>> GetMajorsAsync(
        MajorFilter filter, CancellationToken cancellationToken = default);

    Task<int> CreateMajorAsync(MajorDto major, CancellationToken cancellationToken = default);
    Task UpdateMajorAsync(MajorDto major, CancellationToken cancellationToken = default);
    Task DeactivateMajorAsync(int majorId, CancellationToken cancellationToken = default);

    // ----- Manage Semester -----
    Task<int> CreateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default);
    Task UpdateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a semester as current. The implementation must clear the flag on
    /// the previous one inside the SAME transaction: the database assumes
    /// exactly one semester has IsCurrent = 1, and db/02_seed_master.sql
    /// asserts it.
    /// </summary>
    Task SetCurrentSemesterAsync(int semesterId, CancellationToken cancellationToken = default);

    // ----- Manage Subject -----

    /// <summary>Subjects for the admin list, with search and filtering.</summary>
    Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        CourseFilter filter, CancellationToken cancellationToken = default);

    Task<CourseDto?> GetCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<int> CreateCourseAsync(CourseDto course, CancellationToken cancellationToken = default);
    Task UpdateCourseAsync(CourseDto course, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a course (IsActive = false) instead of deleting it.
    /// A hard delete would take the historical transcripts of every student who
    /// ever took the course down with it.
    /// </summary>
    Task DeactivateCourseAsync(int courseId, CancellationToken cancellationToken = default);

    // ----- Assign Subject to Major / Curriculum Management -----
    Task<int> AssignCourseToMajorAsync(int majorId, int courseId, int termNo, bool isMandatory, CancellationToken cancellationToken = default);
    Task RemoveCourseFromMajorAsync(int curriculumId, CancellationToken cancellationToken = default);
    Task UpdateCurriculumItemAsync(int curriculumId, int termNo, bool isMandatory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes Major.RequiredCredits from the current curriculum.
    /// MUST be called after every add or remove: if the two disagree, the
    /// graduation percentage is wrong for EVERY student in that major.
    /// </summary>
    Task SyncMajorRequiredCreditsAsync(int majorId, CancellationToken cancellationToken = default);

    // ----- Prerequisites -----
    Task<int> AddPrerequisiteAsync(int courseId, int requiredCourseId, CancellationToken cancellationToken = default);
    Task RemovePrerequisiteAsync(int prerequisiteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a set of ALTERNATIVES: passing any one of
    /// <paramref name="requiredCourseIds"/> satisfies the requirement.
    ///
    /// Real syllabi need this - MKT205c asks for "MKT101 or MKG101 or MMK101" -
    /// and adding them one at a time through
    /// <see cref="AddPrerequisiteAsync"/> would wrongly demand all three.
    /// </summary>
    Task<int> AddPrerequisiteGroupAsync(
        int courseId, IReadOnlyList<int> requiredCourseIds, CancellationToken cancellationToken = default);

    /// <summary>A subject's direct prerequisites, grouped as stored.</summary>
    Task<IReadOnlyList<PrerequisiteEdgeDto>> GetPrerequisitesAsync(
        int courseId, CancellationToken cancellationToken = default);
}
