namespace FAT.Domain.Entities;

/// <summary>
/// A reading or reference attached to a subject: a title, a description and a
/// link. This is the syllabus bibliography as published by FLM.
///
/// DELIBERATELY SEPARATE FROM <see cref="Material"/>. That one stores uploaded
/// FILES - it requires FileName, ContentType and a non-empty byte[] in
/// MaterialFile, and the database caps it at 25 MB. A textbook reference has no
/// bytes to store, so forcing it through Material would mean making three
/// required columns nullable and dropping the size CHECK, which would break the
/// upload feature that relies on them.
/// </summary>
public class SubjectMaterial
{
    public int SubjectMaterialId { get; set; }
    public int CourseId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Absolute http/https link. Null for a printed book with no online copy.</summary>
    public string? Url { get; set; }

    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public string? Isbn { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Course? Course { get; set; }
}
