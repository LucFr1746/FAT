using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Personal profile and account administration.
/// FROZEN CONTRACT - owner: Member 1.
///
/// Together with <see cref="IAuthService"/> (Login, Logout, Change Password)
/// this covers the remaining two features, Profile and User Management.
/// </summary>
public interface IUserService
{
    // ----- Profile -----
    Task<StudentProfileDto?> GetProfileAsync(int studentId, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(int studentId, string fullName, string? email, DateTime? dateOfBirth, string? currentSemester = null, string? selectedMajor = null, string? campus = null, CancellationToken cancellationToken = default);
    Task CompleteAcademicProfileAsync(int studentId, int majorId, int currentTermNo, CancellationToken cancellationToken = default);

    // ----- User Management (Admin only) -----
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? keyword = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an account. The implementation hashes the password with BCrypt -
    /// it must never accept a pre-computed hash from a caller and must never
    /// store a plaintext password.
    /// </summary>
    Task<int> CreateUserAsync(string username, string password, string roleName, CancellationToken cancellationToken = default);

    /// <summary>Locks or unlocks an account.</summary>
    Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Admin password reset, without knowing the previous password.</summary>
    Task ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}
