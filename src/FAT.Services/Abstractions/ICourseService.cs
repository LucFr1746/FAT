using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Catalog lookups - READ ONLY.
/// FROZEN CONTRACT - owner: Member 3.
///
/// Serves four of Member 3's five features: Select Major, View Subjects,
/// View Semester and Subject Detail.
///
/// THE READ/WRITE SPLIT IS DELIBERATE: every mutation lives in
/// <see cref="ICatalogAdminService"/>, owned by Member 2. Two people therefore
/// work on the same data while editing different files, which keeps them out of
/// each other's way at merge time.
/// </summary>
public interface ICourseService
{
    /// <summary>View Subjects, with search and filtering.</summary>
    Task<IReadOnlyList<CourseDto>> SearchAsync(CourseFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Subject Detail.</summary>
    Task<CourseDto?> GetByIdAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>Select Major - the list of programmes to choose from.</summary>
    Task<IReadOnlyList<MajorDto>> GetMajorsAsync(CancellationToken cancellationToken = default);

    /// <summary>View Semester.</summary>
    Task<IReadOnlyList<SemesterDto>> GetSemestersAsync(CancellationToken cancellationToken = default);

    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken cancellationToken = default);

    /// <summary>The curriculum of a major, ordered by term.</summary>
    Task<IReadOnlyList<CurriculumItemDto>> GetCurriculumAsync(int majorId, CancellationToken cancellationToken = default);
}
