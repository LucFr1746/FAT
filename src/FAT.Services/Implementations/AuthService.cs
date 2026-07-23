using FAT.Data;
using FAT.Domain.Constants;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>BCrypt-based authentication. Owner: Member 1.</summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Decoy hash used when no account matches the username.
    ///
    /// Returning immediately for a missing account would make "wrong username"
    /// noticeably faster than "wrong password", since BCrypt is deliberately
    /// slow. Timing that difference is enough to enumerate valid usernames.
    /// Verifying against a decoy keeps both paths equally expensive.
    /// </summary>
    private const string DecoyHash = "$2a$11$JJQiWDIKwyl.f89GLxktb.lx2BSbc.XhflOzX9V993TDFW0fQsAzW";

    /// <summary>
    /// One message for EVERY failed sign-in.
    /// It deliberately does not distinguish a bad username from a bad password,
    /// because distinguishing them confirms which accounts exist.
    /// </summary>
    private const string InvalidCredentialsMessage = "Incorrect username or password.";

    private readonly FatDbContext _db;

    public AuthService(FatDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Please enter both a username and a password.");
        }

        var normalized = username.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .SingleOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(password, DecoyHash);
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        // The IsActive check comes AFTER password verification on purpose:
        // checking it first would disclose that an account exists to someone
        // who does not know its password.
        if (!user.IsActive)
        {
            return LoginResult.Failure("This account is locked. Please contact an administrator.");
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
            FullName: user.Student?.FullName ?? user.Username,
            AvatarUrl: user.AvatarUrl));
    }

    public async Task<LoginResult> LoginWithGoogleAsync(GoogleUserInfoDto googleUser, CancellationToken cancellationToken = default)
    {
        if (googleUser == null || string.IsNullOrWhiteSpace(googleUser.Email))
        {
            return LoginResult.Failure("Thông tin Google OAuth không hợp lệ.");
        }

        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.GoogleId == googleUser.GoogleId || (u.Student != null && u.Student.Email != null && u.Student.Email.ToLower() == normalizedEmail), cancellationToken);

        if (user is null)
        {
            // Account not found - return specific message for UI auto-registration trigger
            return LoginResult.Failure("ACCOUNT_NOT_FOUND");
        }

        if (!user.IsActive)
        {
            return LoginResult.Failure("Tài khoản này hiện đang bị khóa. Vui lòng liên hệ Quản trị viên.");
        }

        // Update GoogleId and AvatarUrl if newly linked
        if (string.IsNullOrEmpty(user.GoogleId) || user.AvatarUrl != googleUser.PictureUrl)
        {
            user.GoogleId = googleUser.GoogleId;
            if (!string.IsNullOrEmpty(googleUser.PictureUrl))
            {
                user.AvatarUrl = googleUser.PictureUrl;
            }
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
            FullName: user.Student?.FullName ?? user.Username,
            AvatarUrl: user.AvatarUrl));
    }

    public async Task<LoginResult> RegisterStudentAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            return LoginResult.Failure("Dữ liệu đăng ký không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(dto.StudentCode))
        {
            return LoginResult.Failure("Vui lòng nhập Mã số sinh viên.");
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return LoginResult.Failure("Vui lòng nhập Họ và tên.");
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return LoginResult.Failure("Vui lòng cung cấp Email.");
        }

        if (!dto.AcceptTerms)
        {
            return LoginResult.Failure("Bạn phải đồng ý với các điều khoản dịch vụ để tiếp tục.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            if (dto.Password.Length < 8)
            {
                return LoginResult.Failure("Mật khẩu phải có tối thiểu 8 ký tự.");
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                return LoginResult.Failure("Xác nhận mật khẩu không trùng khớp.");
            }
        }

        var normalizedStudentCode = dto.StudentCode.Trim().ToUpperInvariant();
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        // Check unique StudentCode
        var codeExists = await _db.Students.AnyAsync(s => s.StudentCode == normalizedStudentCode, cancellationToken);
        if (codeExists)
        {
            return LoginResult.Failure($"Mã sinh viên '{normalizedStudentCode}' đã tồn tại trong hệ thống.");
        }

        // Check unique Email
        var emailExists = await _db.Students.AnyAsync(s => s.Email != null && s.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return LoginResult.Failure($"Email '{dto.Email}' đã được đăng ký cho một sinh viên khác.");
        }

        // Fetch Student Role
        var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, cancellationToken)
                         ?? await _db.Roles.FirstAsync(cancellationToken);

        // Create AppUser
        var newUser = new Domain.Entities.AppUser
        {
            Username = normalizedStudentCode,
            PasswordHash = !string.IsNullOrWhiteSpace(dto.Password) ? BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11) : null,
            RoleId = studentRole.RoleId,
            GoogleId = dto.GoogleId,
            AvatarUrl = dto.AvatarUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(cancellationToken);

        // Major ID selection
        var majorId = dto.MajorId > 0 ? dto.MajorId : (await _db.Majors.Select(m => m.MajorId).FirstOrDefaultAsync(cancellationToken));
        if (majorId == 0) majorId = 1;

        // Create Student Profile
        var newStudent = new Domain.Entities.Student
        {
            UserId = newUser.UserId,
            StudentCode = normalizedStudentCode,
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            EnrollmentDate = DateTime.Today,
            MajorId = majorId,
            Status = Domain.Enums.StudentStatus.Active
        };

        _db.Students.Add(newStudent);
        await _db.SaveChangesAsync(cancellationToken);

        return LoginResult.Success(new CurrentUserInfo(
            UserId: newUser.UserId,
            Username: newUser.Username,
            RoleName: studentRole.RoleName,
            IsAdmin: false,
            StudentId: newStudent.StudentId,
            StudentCode: newStudent.StudentCode,
            FullName: newStudent.FullName,
            AvatarUrl: newUser.AvatarUrl));
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("The new password must be at least 8 characters long.", nameof(newPassword));
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null || (user.PasswordHash != null && !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

