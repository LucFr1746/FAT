namespace Domain.Entities;

/// <summary>
/// A learning material - the METADATA only (no file bytes).
///
/// The binary content lives in <see cref="MaterialFile"/> so that listing and
/// searching never has to pull binary data across the wire.
/// </summary>
public class Material
{
    public int MaterialId { get; set; }

    /// <summary>Null means a general material not tied to any course.</summary>
    public int? CourseId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Slide | Textbook | Exercise | Exam | Reference | Other.</summary>
    public string Category { get; set; } = "Other";

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 of the content, used to detect duplicate uploads.</summary>
    public string? ContentHash { get; set; }

    public int? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int DownloadCount { get; set; }
    public bool IsActive { get; set; } = true;

    public Course? Course { get; set; }
    public AppUser? UploadedBy { get; set; }

    /// <summary>
    /// Load this only when the user actually clicks Download.
    /// NEVER Include() this navigation property in a list query.
    /// </summary>
    public MaterialFile? File { get; set; }
}
