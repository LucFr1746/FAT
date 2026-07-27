using FAT.Services.Dtos;

namespace FAT.Services.Abstractions;

/// <summary>
/// User authentication. FROZEN CONTRACT - owner: Member 1.
/// Covers the Login, Logout and Change Password features.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates credentials and returns the user when they are correct.
    /// Does not throw on a wrong password - a wrong password is an ordinary
    /// business outcome, not an exceptional condition.
    /// </summary>
    Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user by matching their GoogleId or Email in DB.
    /// If existing, updates GoogleId/AvatarUrl and returns the CurrentUserInfo.
    /// If not registered, returns a failure result prompting registration.
    /// </summary>
    Task<LoginResult> LoginWithGoogleAsync(GoogleUserInfoDto googleUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new Student profile along with an AppUser login account.
    /// </summary>
    Task<LoginResult> RegisterStudentAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Changes a password after verifying the current one.</summary>
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}

