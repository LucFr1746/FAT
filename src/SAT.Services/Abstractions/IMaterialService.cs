using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Quản lý tài liệu học tập.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 5.
///
/// Phụ trách 5 chức năng: Manage Materials, Upload, Download,
/// View Materials, Search Materials.
/// </summary>
public interface IMaterialService
{
    /// <summary>Trần kích thước một file, khớp với ràng buộc CHECK trong DB.</summary>
    const long MaxFileSizeBytes = 25L * 1024 * 1024;

    /// <summary>View + Search Materials. Không nạp nội dung file.</summary>
    Task<IReadOnlyList<MaterialDto>> SearchAsync(MaterialFilter filter, CancellationToken cancellationToken = default);

    Task<MaterialDto?> GetByIdAsync(int materialId, CancellationToken cancellationToken = default);

    /// <summary>Tài liệu của một môn học cụ thể.</summary>
    Task<IReadOnlyList<MaterialDto>> GetByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload. Trả về MaterialId mới.
    ///
    /// Cài đặt PHẢI kiểm tra trước khi ghi:
    ///   - Kích thước không vượt <see cref="MaxFileSizeBytes"/>.
    ///   - Category nằm trong <see cref="MaterialCategories.All"/>.
    ///   - Tính SHA-256 và cảnh báo nếu đã có file trùng nội dung.
    ///   - Làm sạch FileName (bỏ ký tự đường dẫn) trước khi lưu: tên file do
    ///     người dùng đặt mà mang theo "..\" sẽ thành lỗ hổng khi lưu ra đĩa.
    /// </summary>
    Task<int> UploadAsync(MaterialUploadRequest request, int uploadedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download. Chỉ hàm này được đụng tới bảng MaterialFile.
    /// Đồng thời tăng DownloadCount.
    /// </summary>
    Task<MaterialDownload?> DownloadAsync(int materialId, CancellationToken cancellationToken = default);

    Task UpdateAsync(int materialId, string title, string? description, string category, int? courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vô hiệu hóa tài liệu (IsActive = false) thay vì xóa cứng, để lịch sử
    /// tải xuống và tham chiếu không bị mất.
    /// </summary>
    Task DeactivateAsync(int materialId, CancellationToken cancellationToken = default);
}
