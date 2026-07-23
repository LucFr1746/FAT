using FAT.Services.Abstractions;

namespace FAT.Services.Implementations;

/// <summary>
/// The curriculum members of the frozen <c>ICatalogAdminService</c> contract.
///
/// These FORWARD to <see cref="CurriculumAdminService"/> rather than
/// reimplementing anything. The contract was frozen before the richer curriculum
/// features (bulk assign, reorder, export) existed, and two copies of "add a
/// subject to a programme" would eventually disagree about whether to
/// resynchronise Major.RequiredCredits - which is the one mistake that quietly
/// breaks every student's graduation percentage.
/// </summary>
public sealed partial class CatalogAdminService
{
    public Task<int> AssignCourseToMajorAsync(
        int majorId, int courseId, int termNo, bool isMandatory,
        CancellationToken cancellationToken = default)
        => _curriculumAdmin.AssignAsync(majorId, courseId, termNo, isMandatory, cancellationToken);

    public Task RemoveCourseFromMajorAsync(int curriculumId, CancellationToken cancellationToken = default)
        => _curriculumAdmin.RemoveAsync(curriculumId, cancellationToken);

    public Task UpdateCurriculumItemAsync(
        int curriculumId, int termNo, bool isMandatory, CancellationToken cancellationToken = default)
        => _curriculumAdmin.UpdateItemAsync(curriculumId, termNo, isMandatory, cancellationToken);

    /// <summary>
    /// Recomputes Major.RequiredCredits and TotalTerms from the curriculum.
    ///
    /// The other mutating methods already do this as part of their own unit of
    /// work; this stays public because the contract exposes it and because a
    /// database repaired by hand needs a way to be brought back into line.
    /// </summary>
    public async Task SyncMajorRequiredCreditsAsync(
        int majorId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Đồng bộ số tín chỉ của ngành");

        await MajorCreditCalculator.SyncAsync(_db, majorId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
