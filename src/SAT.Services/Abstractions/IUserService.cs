using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Hồ sơ cá nhân và quản trị tài khoản.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 1.
///
/// Cùng với <see cref="IAuthService"/> (Login, Logout, Change Password),
/// interface này phủ nốt Profile và User Management - đủ 5 chức năng.
/// </summary>
public interface IUserService
{
    // ----- Profile -----
    Task<StudentProfileDto?> GetProfileAsync(int studentId, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(int studentId, string fullName, string? email, DateTime? dateOfBirth, CancellationToken cancellationToken = default);

    // ----- User Management (chỉ Admin) -----
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? keyword = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo tài khoản. Mật khẩu được hash bằng BCrypt trong cài đặt - TUYỆT ĐỐI
    /// không nhận sẵn hash từ bên ngoài và không lưu mật khẩu thô.
    /// </summary>
    Task<int> CreateUserAsync(string username, string password, string roleName, CancellationToken cancellationToken = default);

    /// <summary>Khóa / mở khóa tài khoản.</summary>
    Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Admin đặt lại mật khẩu mà không cần biết mật khẩu cũ.</summary>
    Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}
