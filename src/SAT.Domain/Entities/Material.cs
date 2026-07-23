namespace SAT.Domain.Entities;

/// <summary>
/// Tài liệu học tập - phần MÔ TẢ (không chứa byte của file).
///
/// Nội dung file nằm ở <see cref="MaterialFile"/> tách riêng, để màn hình
/// danh sách và tìm kiếm không bao giờ phải kéo dữ liệu nhị phân về máy.
/// </summary>
public class Material
{
    public int MaterialId { get; set; }

    /// <summary>Null nghĩa là tài liệu dùng chung, không thuộc môn nào.</summary>
    public int? CourseId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Slide | Textbook | Exercise | Exam | Reference | Other.</summary>
    public string Category { get; set; } = "Other";

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>SHA-256 của nội dung, dùng để phát hiện tải lên trùng file.</summary>
    public string? ContentHash { get; set; }

    public int? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int DownloadCount { get; set; }
    public bool IsActive { get; set; } = true;

    public Course? Course { get; set; }
    public AppUser? UploadedBy { get; set; }

    /// <summary>
    /// Chỉ nạp khi người dùng thực sự bấm Tải xuống.
    /// KHÔNG bao giờ Include() thuộc tính này trong truy vấn danh sách.
    /// </summary>
    public MaterialFile? File { get; set; }
}
