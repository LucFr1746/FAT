using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Loads the catalog from an FLM export - the Excel workbook or the JSON folder.
///
/// IDEMPOTENT BY CONTRACT. Running the same import twice must leave the database
/// in the same state as running it once: every row is matched on its natural key
/// (major code, subject code, major+subject, subject+component name, ...) and
/// updated rather than inserted a second time. An import that duplicates the
/// catalog is worse than no import at all, because Major.RequiredCredits then
/// doubles and every student's graduation percentage halves.
/// </summary>
public interface IFlmImportService
{
    /// <summary>
    /// Reads the file and reports what WOULD be imported. Writes nothing.
    /// Always offer this before the real thing - an import touches the whole
    /// catalog and there is no undo.
    /// </summary>
    Task<ImportPreviewDto> PreviewAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the file. Admin only.
    ///
    /// Recomputes Major.RequiredCredits for every major it touches, because a
    /// curriculum that changed without that recalculation leaves the graduation
    /// percentage wrong for every student in the programme.
    /// </summary>
    Task<ImportResultDto> ImportAsync(
        string path,
        ImportOptions? options = null,
        CancellationToken cancellationToken = default);
}
