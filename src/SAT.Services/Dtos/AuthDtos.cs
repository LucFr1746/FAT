namespace SAT.Services.Dtos;

/// <summary>Kết quả một lần đăng nhập.</summary>
/// <remarks>
/// Khi thất bại, <see cref="ErrorMessage"/> cố ý KHÔNG nói rõ là sai tài khoản
/// hay sai mật khẩu: nói rõ sẽ giúp người ngoài dò được username nào có thật.
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
/// Thông tin người dùng đang đăng nhập, dùng khắp ứng dụng.
/// Cố ý KHÔNG chứa PasswordHash - dữ liệu này đi tới tận ViewModel.
/// </summary>
public sealed record CurrentUserInfo(
    int UserId,
    string Username,
    string RoleName,
    bool IsAdmin,
    // StudentId null với tài khoản Admin, vì Admin không phải sinh viên.
    int? StudentId,
    string? StudentCode,
    string? FullName);
