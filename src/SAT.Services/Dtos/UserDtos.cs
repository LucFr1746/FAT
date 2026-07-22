using SAT.Domain.Enums;

namespace SAT.Services.Dtos;

/// <summary>
/// Tài khoản ở màn hình User Management.
/// Cố ý KHÔNG có PasswordHash: DTO này đi tới tận ViewModel và binding, không
/// có lý do gì để hash mật khẩu đi cùng nó.
/// </summary>
public sealed record UserDto(
    int UserId,
    string Username,
    string RoleName,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    string? StudentCode,
    string? FullName);

/// <summary>Hồ sơ cá nhân của sinh viên (màn hình Profile).</summary>
public sealed record StudentProfileDto(
    int StudentId,
    string StudentCode,
    string FullName,
    string? Email,
    DateTime? DateOfBirth,
    DateTime EnrollmentDate,
    int MajorId,
    string MajorCode,
    string MajorName,
    StudentStatus Status,
    string Username);

/// <summary>Ngành đào tạo.</summary>
public sealed record MajorDto(
    int MajorId,
    string MajorCode,
    string MajorName,
    int RequiredCredits,
    int TotalTerms,
    bool IsActive);
