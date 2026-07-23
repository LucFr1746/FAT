using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// CRUD over a subject's readings and links.
///
/// Distinct from <see cref="IMaterialService"/>, which manages UPLOADED FILES
/// (Member 5's module). This one carries no bytes - only a title, a description
/// and a URL.
/// </summary>
public interface ISubjectMaterialService
{
    /// <summary>A subject's readings, in display order.</summary>
    Task<IReadOnlyList<SubjectMaterialDto>> GetByCourseAsync(
        int courseId, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(SubjectMaterialDto material, CancellationToken cancellationToken = default);

    Task UpdateAsync(SubjectMaterialDto material, CancellationToken cancellationToken = default);

    Task DeleteAsync(int subjectMaterialId, CancellationToken cancellationToken = default);

    /// <summary>Rewrites the display order from the given sequence of ids.</summary>
    Task ReorderAsync(
        int courseId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);
}
