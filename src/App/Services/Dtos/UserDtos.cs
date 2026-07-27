using Domain.Enums;

namespace Services.Dtos;

/// <summary>
/// An account as shown on the User Management screen.
/// Deliberately has no PasswordHash: this record travels all the way to the
/// view and there is no reason for a hash to travel with it.
/// </summary>
public sealed record UserDto(
    int UserId,
    string Username,
    string RoleName,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    string? StudentCode,
    string? FullName,
    string? MajorName = null);

/// <summary>A student's own profile (the Profile screen).</summary>
public sealed record StudentProfileDto(
    int StudentId,
    string StudentCode,
    string FullName,
    string? Email,
    string? Phone,
    string? ClassName,
    DateTime? DateOfBirth,
    DateTime EnrollmentDate,
    int MajorId,
    string MajorCode,
    string MajorName,
    StudentStatus Status,
    string Username,
    string? CurrentSemester = "Kỳ 5",
    string? Campus = "Đà Nẵng",
    bool IsProfileCompleted = false);

/// <summary>A degree programme.</summary>
public sealed record MajorDto(
    int MajorId,
    string MajorCode,
    string MajorName,
    int RequiredCredits,
    int TotalTerms,
    bool IsActive);
