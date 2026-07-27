namespace Services.Dtos;

/// <summary>
/// One material shown in the library browser (Member 5's module).
///
/// This is a READ projection over <c>SubjectMaterial</c> joined to its course.
/// The module deliberately reuses the syllabus links already imported from FLM
/// rather than storing file bytes: a material here is a title plus a link the
/// student clicks to download.
/// </summary>
public sealed record MaterialLibraryItemDto(
    int SubjectMaterialId,
    int? UploadedMaterialId,
    int CourseId,
    string CourseCode,
    string CourseName,
    string Title,
    string? Description,
    string? Url,
    string? Author,
    string? Publisher,
    string? Isbn,
    string? FileName = null,
    long? FileSizeBytes = null)
{
    /// <summary>True for a file uploaded into the app (bytes in the DB), not an FLM link.</summary>
    public bool IsUploadedFile => UploadedMaterialId is not null;

    /// <summary>True when there is a link to open; false for a printed book with no online copy.</summary>
    public bool HasLink => !string.IsNullOrWhiteSpace(Url);

    /// <summary>Whether the "Tải xuống" button does anything: open a link or save a file.</summary>
    public bool CanDownload => HasLink || IsUploadedFile;

    /// <summary>"MSSV - Tên môn" style label, so a row makes sense on its own.</summary>
    public string SubjectDisplay => $"{CourseCode} - {CourseName}";

    /// <summary>Author column text: the real author for a link, a tag for an uploaded file.</summary>
    public string? AuthorDisplay => IsUploadedFile ? "(Tệp tải lên)" : Author;
}

/// <summary>Filter applied on the material library screen (View + Search).</summary>
public sealed record MaterialLibraryFilter(
    string? Keyword = null,
    int? CourseId = null,
    bool OnlyDownloadable = false,
    int? MajorId = null,
    int? TermNo = null);

/// <summary>One term option for the library's "Kỳ học" filter dropdown.</summary>
public sealed record MaterialTermOptionDto(int? TermNo, string Display)
{
    /// <summary>The "no term filter" sentinel used as the dropdown's default row.</summary>
    public static MaterialTermOptionDto All { get; } = new(null, "Tất cả kỳ");

    public bool IsAll => TermNo is null;
}

/// <summary>One major option for the library's "Ngành học" filter dropdown.</summary>
public sealed record MaterialMajorOptionDto(int? MajorId, string MajorCode, string MajorName)
{
    /// <summary>The "no major filter" sentinel used as the dropdown's default row.</summary>
    public static MaterialMajorOptionDto All { get; } = new(null, string.Empty, "Tất cả ngành");

    public bool IsAll => MajorId is null;

    public string Display => IsAll ? MajorName : $"{MajorCode} - {MajorName}";
}

/// <summary>One subject option for the library's filter dropdown.</summary>
public sealed record MaterialSubjectOptionDto(int CourseId, string CourseCode, string CourseName)
{
    /// <summary>The "no subject filter" sentinel used as the dropdown's default row.</summary>
    public static MaterialSubjectOptionDto All { get; } = new(0, string.Empty, "Tất cả môn học");

    /// <summary>True for the <see cref="All"/> sentinel; it means "do not filter by subject".</summary>
    public bool IsAll => CourseId == 0;

    public string Display => IsAll ? CourseName : $"{CourseCode} - {CourseName}";
}
