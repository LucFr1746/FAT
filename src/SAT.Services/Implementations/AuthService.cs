using Microsoft.EntityFrameworkCore;
using SAT.Data;
using SAT.Domain.Constants;
using SAT.Services.Abstractions;
using SAT.Services.Dtos;

namespace SAT.Services.Implementations;

/// <summary>Xác thực bằng BCrypt. Chủ sở hữu: TV1.</summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Hash giả dùng khi không tìm thấy tài khoản.
    ///
    /// Nếu tài khoản không tồn tại mà trả về ngay lập tức, thì thời gian phản
    /// hồi của "sai username" sẽ ngắn hơn hẳn "sai password" (BCrypt cố tình
    /// chạy chậm). Người ngoài đo thời gian là dò được username nào có thật.
    /// Verify với hash giả để hai nhánh tốn thời gian ngang nhau.
    /// </summary>
    private const string DummyHash = "$2a$11$JJQiWDIKwyl.f89GLxktb.lx2BSbc.XhflOzX9V993TDFW0fQsAzW";

    /// <summary>
    /// Thông báo lỗi chung cho MỌI trường hợp đăng nhập thất bại.
    /// Cố ý không phân biệt "sai tài khoản" với "sai mật khẩu": phân biệt sẽ
    /// giúp người ngoài xác nhận được username nào tồn tại trong hệ thống.
    /// </summary>
    private const string InvalidCredentialsMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";

    private readonly SatDbContext _db;

    public AuthService(SatDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");
        }

        var normalized = username.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .SingleOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash);
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        // Kiểm tra IsActive SAU khi đã verify mật khẩu. Kiểm tra trước sẽ để lộ
        // việc tài khoản có tồn tại hay không cho người không biết mật khẩu.
        if (!user.IsActive)
        {
            return LoginResult.Failure("Tài khoản đã bị khóa. Liên hệ quản trị viên.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var roleName = user.Role?.RoleName ?? RoleNames.Student;

        return LoginResult.Success(new CurrentUserInfo(
            UserId: user.UserId,
            Username: user.Username,
            RoleName: roleName,
            IsAdmin: string.Equals(roleName, RoleNames.Admin, StringComparison.Ordinal),
            StudentId: user.Student?.StudentId,
            StudentCode: user.Student?.StudentCode,
            FullName: user.Student?.FullName ?? user.Username));
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("Mật khẩu mới phải có ít nhất 8 ký tự.", nameof(newPassword));
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
