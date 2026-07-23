namespace SAT.Services.Dtos;

/// <summary>
/// Tài liệu ở dạng hiển thị trên danh sách.
/// KHÔNG chứa byte của file - nội dung chỉ được nạp khi bấm Tải xuống.
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
    /// <summary>Kích thước ở dạng người đọc được, ví dụ "1,4 MB".</summary>
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:0.#} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):0.#} MB"
    };
}

/// <summary>Bộ lọc của màn hình tìm kiếm tài liệu.</summary>
public sealed record MaterialFilter(
    string? Keyword = null,
    int? CourseId = null,
    string? Category = null,
    bool IncludeInactive = false);

/// <summary>Dữ liệu để tải một tài liệu mới lên.</summary>
public sealed record MaterialUploadRequest(
    int? CourseId,
    string Title,
    string? Description,
    string Category,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>Nội dung tài liệu trả về khi tải xuống.</summary>
public sealed record MaterialDownload(string FileName, string ContentType, byte[] Content);

/// <summary>Các nhóm tài liệu hợp lệ.</summary>
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
