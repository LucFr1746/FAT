using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Xác thực người dùng. 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: TV1.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Kiểm tra tài khoản/mật khẩu và trả về thông tin người dùng nếu hợp lệ.
    /// Không ném exception khi sai mật khẩu - sai mật khẩu là kết quả bình
    /// thường của nghiệp vụ, không phải sự cố.
    /// </summary>
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Đổi mật khẩu, có kiểm tra mật khẩu hiện tại.</summary>
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
