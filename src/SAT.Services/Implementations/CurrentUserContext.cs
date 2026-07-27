using SAT.Services.Abstractions;
using SAT.Services.Dtos;

namespace SAT.Services.Implementations;

/// <summary>
/// Giữ người dùng đang đăng nhập. Đăng ký SINGLETON: cả ứng dụng chỉ có
/// một phiên đăng nhập tại một thời điểm. Chủ sở hữu: TV1.
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    public CurrentUserInfo? User { get; private set; }

    public bool IsAuthenticated => User is not null;

    public bool IsAdmin => User?.IsAdmin ?? false;

    public int? StudentId => User?.StudentId;

    public event EventHandler? UserChanged;

    public void SetUser(CurrentUserInfo user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        User = null;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public int RequireStudentId()
        => StudentId ?? throw new InvalidOperationException(
            "Màn hình này chỉ dành cho sinh viên, nhưng tài khoản đang đăng nhập không gắn hồ sơ sinh viên nào.");
}
