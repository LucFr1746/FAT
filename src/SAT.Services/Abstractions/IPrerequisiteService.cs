using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Kiểm tra điều kiện tiên quyết.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 3.
/// Phục vụ hai chức năng Subject Detail và Curriculum Progress.
/// </summary>
public interface IPrerequisiteService
{
    /// <summary>Danh sách môn tiên quyết trực tiếp (chỉ một tầng).</summary>
    Task<IReadOnlyList<CourseDto>> GetDirectPrerequisitesAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cây tiên quyết đầy đủ, đệ quy nhiều tầng.
    ///
    /// CÀI ĐẶT BẮT BUỘC CHỐNG CHU TRÌNH: dùng HashSet đánh dấu môn đã duyệt.
    /// Dữ liệu sai (A cần B, B cần A) mà không chặn thì đệ quy vô hạn và app
    /// treo cứng - đúng vào lúc demo. Ràng buộc DB chỉ chặn được chu trình
    /// độ dài 1, không chặn được chu trình dài hơn.
    /// </summary>
    Task<PrerequisiteNodeDto> GetPrerequisiteTreeAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh viên đã đủ điều kiện học môn này chưa.
    /// Luôn trả về kèm danh sách môn còn thiếu để hiển thị lý do.
    /// </summary>
    Task<PrerequisiteCheckResult> CanEnrollAsync(int studentId, int courseId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra hàng loạt - dùng cho Planner để lọc môn khả dụng.</summary>
    Task<IReadOnlyDictionary<int, PrerequisiteCheckResult>> CanEnrollManyAsync(
        int studentId, IEnumerable<int> courseIds, CancellationToken cancellationToken = default);
}
