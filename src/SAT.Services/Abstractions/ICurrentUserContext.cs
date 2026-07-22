using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Giữ thông tin người đang đăng nhập cho toàn phiên làm việc.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: TV1.
///
/// MỌI ViewModel lấy StudentId từ đây, TUYỆT ĐỐI không tự truyền StudentId
/// qua tham số điều hướng. Nếu để màn hình tự quyết định xem dữ liệu của ai
/// thì chỉ cần một chỗ truyền sai là sinh viên này xem được điểm sinh viên kia.
/// </summary>
public interface ICurrentUserContext
{
    CurrentUserInfo? User { get; }

    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    /// <summary>StudentId của người đang đăng nhập, null nếu là Admin.</summary>
    int? StudentId { get; }

    /// <summary>Phát sinh khi đăng nhập hoặc đăng xuất, để shell vẽ lại menu.</summary>
    event EventHandler? UserChanged;

    void SetUser(CurrentUserInfo user);
    void Clear();

    /// <summary>
    /// Lấy StudentId, ném lỗi nếu không có. Dùng ở các màn hình chỉ dành cho
    /// sinh viên, để lỗi lộ ra ngay thay vì âm thầm trả về bảng điểm rỗng.
    /// </summary>
    int RequireStudentId();
}
