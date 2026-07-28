using Data;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Handles student profiles and user account administration.
/// </summary>
public class UserService : IUserService
{
    private readonly FAT_DBContext _db;

    public UserService(FAT_DBContext db)
    {
        _db = db;
    }

    public async Task<StudentProfileDto?> GetProfileAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await _db.Students
            .Include(s => s.Major)
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

        if (student == null)
        {
            return null;
        }

        int termNo = student.CurrentTermNo ?? 1;
        if (!student.CurrentTermNo.HasValue && !string.IsNullOrWhiteSpace(student.CurrentSemester) &&
            student.CurrentSemester.StartsWith("Kỳ ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(student.CurrentSemester.Substring(3).Trim(), out var parsedTerm) &&
            parsedTerm >= 1 && parsedTerm <= 9)
        {
            termNo = parsedTerm;
        }

        var semesterStr = !string.IsNullOrWhiteSpace(student.CurrentSemester)
            ? student.CurrentSemester
            : CatalogRules.GetTermName(termNo);

        return new StudentProfileDto(
            StudentId: student.StudentId,
            StudentCode: student.StudentCode,
            FullName: student.FullName,
            Email: student.Email,
            Phone: student.Phone,
            ClassName: student.ClassName,
            DateOfBirth: student.DateOfBirth,
            EnrollmentDate: student.EnrollmentDate,
            MajorId: student.MajorId,
            MajorCode: student.Major?.MajorCode ?? string.Empty,
            MajorName: student.Major?.MajorName ?? string.Empty,
            Status: student.Status,
            Username: student.User?.Username ?? student.StudentCode,
            CurrentSemester: semesterStr,
            Campus: string.IsNullOrWhiteSpace(student.Campus) ? "Hồ Chí Minh" : student.Campus,
            IsProfileCompleted: student.IsProfileCompleted,
            CurrentTermNo: termNo
        );
    }

    public async Task UpdateProfileAsync(int studentId, string fullName, string? email, DateTime? dateOfBirth, string? currentSemester = null, string? selectedMajor = null, string? campus = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Họ và tên không được để trống.", nameof(fullName));
        }

        var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy thông tin sinh viên.");
        student.FullName = fullName.Trim();
        student.Email = email?.Trim();
        student.DateOfBirth = dateOfBirth;

        if (!string.IsNullOrWhiteSpace(currentSemester))
        {
            student.CurrentSemester = currentSemester.Trim();
            if (currentSemester.StartsWith("Kỳ ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(currentSemester.Substring(3).Trim(), out var parsedTermNo) &&
                parsedTermNo >= 1 && parsedTermNo <= 9)
            {
                student.CurrentTermNo = parsedTermNo;
            }
        }

        if (!string.IsNullOrWhiteSpace(campus))
        {
            student.Campus = campus.Trim();
        }

        if (!string.IsNullOrWhiteSpace(selectedMajor))
        {
            var major = await _db.Majors.FirstOrDefaultAsync(m => m.MajorName == selectedMajor.Trim() || m.MajorCode == selectedMajor.Trim(), cancellationToken);
            if (major != null)
            {
                student.MajorId = major.MajorId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAcademicProfileAsync(int studentId, string fullName, string email, string phone, int majorId, string className, int currentTermNo = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Họ và tên không được để trống.", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email không được để trống.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Số điện thoại không được để trống.", nameof(phone));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("Lớp học không được để trống.", nameof(className));
        }

        var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy thông tin sinh viên.");

        var major = await _db.Majors.FirstOrDefaultAsync(m => m.MajorId == majorId, cancellationToken)
            ?? throw new InvalidOperationException("Ngành học được chọn không tồn tại.");

        student.FullName = fullName.Trim();
        student.Email = email.Trim().ToLowerInvariant();
        student.Phone = phone.Trim();
        student.MajorId = major.MajorId;
        student.ClassName = className.Trim();
        student.CurrentTermNo = currentTermNo;
        student.CurrentSemester = CatalogRules.GetTermName(currentTermNo);
        student.IsProfileCompleted = true;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
                .ThenInclude(s => s!.Major)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(k)
                                  || (u.Role != null && u.Role.RoleName.ToLower().Contains(k))
                                  || (u.Student != null && u.Student.FullName.ToLower().Contains(k))
                                  || (u.Student != null && u.Student.StudentCode.ToLower().Contains(k))
                                  || (u.Student != null && u.Student.Major != null && u.Student.Major.MajorName.ToLower().Contains(k)));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto(
                u.UserId,
                u.Username,
                u.Role != null ? u.Role.RoleName : "Student",
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt,
                u.Student != null ? u.Student.StudentCode : null,
                u.Student != null ? u.Student.FullName : u.Username,
                u.Student != null && u.Student.Major != null ? u.Student.Major.MajorName : "N/A"
            ))
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<int> CreateUserAsync(string username, string password, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Username và Password không được để trống.");
        }

        var normalizedUsername = username.Trim();
        var exists = await _db.Users.AnyAsync(u => u.Username == normalizedUsername, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Tài khoản '{normalizedUsername}' đã tồn tại.");
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName, cancellationToken)
                   ?? await _db.Roles.FirstAsync(cancellationToken);

        var newUser = new AppUser
        {
            Username = normalizedUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
            RoleId = role.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(cancellationToken);

        return newUser.UserId;
    }

    public async Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("Mật khẩu mới phải có tối thiểu 8 ký tự.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken) ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
