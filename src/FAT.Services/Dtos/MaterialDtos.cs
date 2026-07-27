namespace FAT.Services.Dtos;

/// <summary>
/// A material as displayed in a list.
/// Carries no file bytes - content is loaded only when Download is clicked.
/// </summary>
public sealed record MaterialDto(
    int MaterialId,
    int? CourseId,
    string? CourseCode,
    string Title,
    string? Description,
    string Category,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? UploadedByUsername,
    DateTime UploadedAt,
    int DownloadCount)
{
    /// <summary>Human-readable size, for example "1.4 MB".</summary>
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:0.#} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):0.#} MB"
    };
}

/// <summary>Filter applied on the material search screen.</summary>
public sealed record MaterialFilter(
    string? Keyword = null,
    int? CourseId = null,
    string? Category = null,
    bool IncludeInactive = false);

/// <summary>Payload for uploading a new material.</summary>
public sealed record MaterialUploadRequest(
    int? CourseId,
    string Title,
    string? Description,
    string Category,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>Material content returned by a download.</summary>
public sealed record MaterialDownload(string FileName, string ContentType, byte[] Content);

/// <summary>The valid material categories.</summary>
public static class MaterialCategories
{
    public const string Slide = "Slide";
    public const string Textbook = "Textbook";
    public const string Exercise = "Exercise";
    public const string Exam = "Exam";
    public const string Reference = "Reference";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All =
        [Slide, Textbook, Exercise, Exam, Reference, Other];

    public static bool IsValid(string? category)
        => !string.IsNullOrWhiteSpace(category) && All.Contains(category);
}
