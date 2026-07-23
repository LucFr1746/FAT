using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// Learning material management. FROZEN CONTRACT - owner: Member 5.
///
/// Covers all five features: Manage Materials, Upload, Download,
/// View Materials, Search Materials.
/// </summary>
public interface IMaterialService
{
    /// <summary>Per-file size cap; matches the CHECK constraint in the database.</summary>
    const long MaxFileSizeBytes = 25L * 1024 * 1024;

    /// <summary>View and Search Materials. Does not load file content.</summary>
    Task<IReadOnlyList<MaterialDto>> SearchAsync(MaterialFilter filter, CancellationToken cancellationToken = default);

    Task<MaterialDto?> GetByIdAsync(int materialId, CancellationToken cancellationToken = default);

    /// <summary>Materials attached to a specific course.</summary>
    Task<IReadOnlyList<MaterialDto>> GetByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload. Returns the new MaterialId.
    ///
    /// The implementation MUST validate before writing:
    ///   - size within <see cref="MaxFileSizeBytes"/>;
    ///   - category present in <see cref="MaterialCategories.All"/>;
    ///   - compute the SHA-256 and warn when identical content already exists;
    ///   - sanitise FileName (strip path separators) before storing it - a
    ///     user-supplied name carrying "..\" becomes a vulnerability the moment
    ///     anything writes it to disk.
    /// </summary>
    Task<int> UploadAsync(MaterialUploadRequest request, int uploadedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download. This is the ONLY method allowed to touch the MaterialFile
    /// table. It also increments DownloadCount.
    /// </summary>
    Task<MaterialDownload?> DownloadAsync(int materialId, CancellationToken cancellationToken = default);

    Task UpdateAsync(int materialId, string title, string? description, string category, int? courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a material (IsActive = false) rather than deleting it, so
    /// download history and references survive.
    /// </summary>
    Task DeactivateAsync(int materialId, CancellationToken cancellationToken = default);
}
