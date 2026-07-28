using Data;
using Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>BCrypt-based authentication. Owner: Member 1.</summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Decoy hash used when no account matches the username.
    /// </summary>
    private const string DecoyHash = "$2a$11$JJQiWDIKwyl.f89GLxktb.lx2BSbc.XhflOzX9V993TDFW0fQsAzW";

    /// <summary>
    /// One message for EVERY failed sign-in.
    /// </summary>
    private const string InvalidCredentialsMessage = "Sai tên đăng nhập hoặc mật khẩu.";

    private readonly FAT_DBContext _db;
    private readonly IEmailService? _emailService;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, OtpRecord> _otpCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed record OtpRecord(string OtpCode, DateTime ExpiresAt, int UserId, int StudentId, string Email, string FullName);

    public AuthService(FAT_DBContext db, IEmailService? emailService = null)
    {
        _db = db;
        _emailService = emailService;
    }


    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Vui lòng nhập đầy đủ MSSV và Mật khẩu.");
        }

        var normalized = username.Trim().ToLowerInvariant();

        // Support login by Username (MSSV / StudentCode)
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .SingleOrDefaultAsync(u => u.Username.ToLower() == normalized
                                   || (u.Student != null && u.Student.StudentCode.ToLower() == normalized)
                                   || (u.Student != null && u.Student.Email != null && u.Student.Email.ToLower() == normalized), cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(password, DecoyHash);
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        if (!user.IsActive)
        {
            return LoginResult.Failure("Tài khoản này đang bị khóa. Vui lòng liên hệ Quản trị viên.");
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
            AvatarUrl: user.AvatarUrl,
            IsProfileCompleted: user.Student?.IsProfileCompleted ?? false));
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
            .FirstOrDefaultAsync(u => (u.GoogleId != null && u.GoogleId == googleUser.GoogleId)
                                   || (u.Student != null && u.Student.Email != null && u.Student.Email.ToLower() == normalizedEmail), cancellationToken);

        if (user is null)
        {
            return LoginResult.Failure("ACCOUNT_NOT_FOUND");
        }

        if (!user.IsActive)
        {
            return LoginResult.Failure("Tài khoản này đang bị khóa. Vui lòng liên hệ Quản trị viên.");
        }

        if (string.IsNullOrEmpty(user.GoogleId))
        {
            user.GoogleId = googleUser.GoogleId;
        }

        if (!string.IsNullOrEmpty(googleUser.PictureUrl))
        {
            user.AvatarUrl = googleUser.PictureUrl;
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
            AvatarUrl: user.AvatarUrl,
            IsProfileCompleted: user.Student?.IsProfileCompleted ?? false));
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

        // Check unique StudentCode / MSSV
        var codeExists = await _db.Students.AnyAsync(s => s.StudentCode == normalizedStudentCode, cancellationToken)
                      || await _db.Users.AnyAsync(u => u.Username == normalizedStudentCode, cancellationToken);
        if (codeExists)
        {
            return LoginResult.Failure($"Mã sinh viên '{normalizedStudentCode}' đã tồn tại trong hệ thống.");
        }

        // Check unique Email if provided
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var emailExists = await _db.Students.AnyAsync(s => s.Email != null && s.Email.ToLower() == normalizedEmail, cancellationToken);
            if (emailExists)
            {
                return LoginResult.Failure($"Email '{dto.Email}' đã được đăng ký cho một sinh viên khác.");
            }
        }

        // Fetch Student Role
        var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleNames.Student, cancellationToken)
                         ?? await _db.Roles.FirstAsync(cancellationToken);

        // Create AppUser & hash password with BCrypt
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
        if (majorId == 0)
        {
            majorId = 1;
        }

        var studentName = !string.IsNullOrWhiteSpace(dto.FullName) ? dto.FullName.Trim() : normalizedStudentCode;
        var studentEmail = !string.IsNullOrWhiteSpace(dto.Email) ? dto.Email.Trim().ToLowerInvariant() : null;

        // Create Student Profile
        var newStudent = new Domain.Entities.Student
        {
            UserId = newUser.UserId,
            StudentCode = normalizedStudentCode,
            FullName = studentName,
            Email = studentEmail,
            Phone = dto.Phone,
            EnrollmentDate = DateTime.Today,
            MajorId = majorId,
            CurrentSemester = "Kỳ 1",
            IsProfileCompleted = false,
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
            AvatarUrl: newUser.AvatarUrl,
            IsProfileCompleted: newStudent.IsProfileCompleted));
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("Mật khẩu mới phải có tối thiểu 8 ký tự.", nameof(newPassword));
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

    public async Task<OtpSendResult> SendResetOtpAsync(string mssvOrEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mssvOrEmail))
        {
            return new OtpSendResult(false, "Vui lòng nhập MSSV hoặc Email.");
        }

        var normalized = mssvOrEmail.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized
                                   || (u.Student != null && u.Student.StudentCode.ToLower() == normalized)
                                   || (u.Student != null && u.Student.Email != null && u.Student.Email.ToLower() == normalized), cancellationToken);

        if (user is null || user.Student is null || string.IsNullOrWhiteSpace(user.Student.Email))
        {
            return new OtpSendResult(false, "Không tìm thấy thông tin sinh viên hoặc Email tương ứng với MSSV này.");
        }

        if (!user.IsActive)
        {
            return new OtpSendResult(false, "Tài khoản của bạn hiện đang bị khóa.");
        }

        var email = user.Student.Email.Trim();
        var fullName = user.Student.FullName ?? user.Username;

        // Generate 6-digit OTP
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        _otpCache[normalized] = new OtpRecord(otpCode, expiresAt, user.UserId, user.Student.StudentId, email, fullName);

        var maskedEmail = MaskEmail(email);

        bool isSentViaSmtp = false;
        if (_emailService != null)
        {
            isSentViaSmtp = await _emailService.SendPasswordResetOtpAsync(email, fullName, otpCode, cancellationToken);
        }

        if (isSentViaSmtp)
        {
            return new OtpSendResult(true, null, maskedEmail, null, true);
        }
        else
        {
            // Dev Demo mode (returns otpCode for UI preview)
            return new OtpSendResult(true, null, maskedEmail, otpCode, false);
        }
    }

    public Task<bool> VerifyResetOtpAsync(string mssvOrEmail, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(mssvOrEmail) || string.IsNullOrWhiteSpace(otpCode))
        {
            return Task.FromResult(false);
        }

        var normalized = mssvOrEmail.Trim().ToLowerInvariant();
        if (!_otpCache.TryGetValue(normalized, out var record))
        {
            return Task.FromResult(false);
        }

        if (record.ExpiresAt < DateTime.UtcNow)
        {
            _otpCache.TryRemove(normalized, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(record.OtpCode.Equals(otpCode.Trim(), StringComparison.Ordinal));
    }

    public async Task<LoginResult> ResetPasswordWithOtpAsync(string mssvOrEmail, string otpCode, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return LoginResult.Failure("Mật khẩu mới phải có tối thiểu 8 ký tự.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"[A-Z]") ||
            !System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"[!@#$%^&*(),.?:{}|<>]"))
        {
            return LoginResult.Failure("Mật khẩu mới phải có ít nhất 1 chữ hoa và 1 ký tự đặc biệt.");
        }

        var isValidOtp = await VerifyResetOtpAsync(mssvOrEmail, otpCode);
        if (!isValidOtp)
        {
            return LoginResult.Failure("Mã OTP không chính xác hoặc đã hết hạn. Vui lòng thử lại.");
        }

        var normalized = mssvOrEmail.Trim().ToLowerInvariant();
        _otpCache.TryGetValue(normalized, out var record);
        if (record is null)
        {
            return LoginResult.Failure("Mã xác thực đã hết hạn.");
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.UserId == record.UserId, cancellationToken);

        if (user is null)
        {
            return LoginResult.Failure("Không tìm thấy tài khoản người dùng.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync(cancellationToken);

        _otpCache.TryRemove(normalized, out _);

        var roleName = user.Role?.RoleName ?? RoleNames.Student;

        return LoginResult.Success(new CurrentUserInfo(
            UserId: user.UserId,
            Username: user.Username,
            RoleName: roleName,
            IsAdmin: roleName == RoleNames.Admin,
            StudentId: user.Student?.StudentId,
            StudentCode: user.Student?.StudentCode,
            FullName: user.Student?.FullName,
            AvatarUrl: user.AvatarUrl,
            IsProfileCompleted: user.Student?.IsProfileCompleted ?? true));
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "***@fpt.edu.vn";
        }

        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];

        if (name.Length <= 3)
        {
            return $"{name[0]}***@{domain}";
        }

        return $"{name[..2]}***{name[^2..]}@{domain}";
    }
}

