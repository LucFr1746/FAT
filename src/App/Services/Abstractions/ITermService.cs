using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// CRUD over the kỳ of the study path - the "Manage Semester" feature.
///
/// NOT to be confused with the calendar semesters (FA25, SP26) managed through
/// <see cref="ICatalogAdminService"/>. See <c>Domain.Entities.Term</c> for
/// why the two are separate.
/// </summary>
public interface ITermService
{
    /// <summary>Every kỳ, ordered by number, each with how many subjects sit in it.</summary>
    Task<IReadOnlyList<TermDto>> GetAllAsync(
        bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<TermDto?> GetByIdAsync(int termId, CancellationToken cancellationToken = default);

    /// <summary>Creates a kỳ. The number must be unique and at least zero.</summary>
    Task<int> CreateAsync(TermDto term, CancellationToken cancellationToken = default);

    Task UpdateAsync(TermDto term, CancellationToken cancellationToken = default);

    Task SetActiveAsync(int termId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a kỳ. REFUSES while any curriculum still points at it - the
    /// foreign key would reject it anyway, and a clear message beats a raw
    /// constraint error.
    /// </summary>
    Task DeleteAsync(int termId, CancellationToken cancellationToken = default);
}
