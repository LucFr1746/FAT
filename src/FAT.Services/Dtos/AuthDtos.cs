namespace FAT.Services.Dtos;

/// <summary>Outcome of a login attempt.</summary>
/// <remarks>
/// On failure, <see cref="ErrorMessage"/> deliberately does NOT reveal whether
/// the username or the password was wrong: doing so lets an outsider enumerate
/// which accounts exist.
/// </remarks>
public sealed record LoginResult(
    bool IsSuccess,
    CurrentUserInfo? User,
    string? ErrorMessage)
{
    public static LoginResult Success(CurrentUserInfo user) => new(true, user, null);
    public static LoginResult Failure(string message) => new(false, null, message);
}

/// <summary>
/// The signed-in user, used throughout the application.
/// Deliberately carries no password hash - this record reaches view models.
/// </summary>
public sealed record CurrentUserInfo(
    int UserId,
    string Username,
    string RoleName,
    bool IsAdmin,
    // StudentId is null for Admin accounts, which have no student profile.
    int? StudentId,
    string? StudentCode,
    string? FullName,
    string? AvatarUrl = null);

/// <summary>Information retrieved from Google OAuth2 UserInfo API.</summary>
public sealed record GoogleUserInfoDto(
    string GoogleId,
    string Email,
    string FullName,
    string? PictureUrl);

/// <summary>Data Transfer Object for registering a student profile.</summary>
public sealed record RegisterRequestDto(
    string StudentCode,
    string FullName,
    string Email,
    string? Faculty,
    int MajorId,
    string? Phone,
    string? Password,
    string? ConfirmPassword,
    bool AcceptTerms,
    string? GoogleId = null,
    string? AvatarUrl = null);

/// <summary>Result of Google OAuth loopback process.</summary>
public sealed record GoogleOAuthResult(
    bool IsSuccess,
    GoogleUserInfoDto? UserInfo,
    string? ErrorMessage);

