using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// The material library - View, Search and Download for Member 5's module.
///
/// It reads the syllabus materials imported from FLM (stored as
/// <c>SubjectMaterial</c>: a title plus an optional download link) and presents
/// them across every subject in one searchable list. "Download" is opening the
/// link in the browser - nothing here stores or serves file bytes, which is why
/// the heavier <see cref="IMaterialService"/> upload contract is left untouched.
/// </summary>
public interface IMaterialLibraryService
{
    /// <summary>View + Search. Active materials only.</summary>
    Task<IReadOnlyList<MaterialLibraryItemDto>> SearchAsync(
        MaterialLibraryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// The subjects that actually have materials, for the filter dropdown.
    /// Narrowed to <paramref name="majorId"/> when a major is chosen (admin);
    /// a student is always narrowed to their own major regardless.
    /// </summary>
    Task<IReadOnlyList<MaterialSubjectOptionDto>> GetSubjectOptionsAsync(
        int? majorId = null, CancellationToken cancellationToken = default);

    /// <summary>The programmes (SE / AI / IB), for the "Ngành học" filter dropdown.</summary>
    Task<IReadOnlyList<MaterialMajorOptionDto>> GetMajorOptionsAsync(
        CancellationToken cancellationToken = default);
}
