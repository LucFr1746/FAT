using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.Services.Abstractions;

namespace FAT.App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Change Password screen.
/// </summary>
public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserContext _currentUserContext;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public ChangePasswordViewModel(IAuthService authService, ICurrentUserContext currentUserContext)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        Title = "Đổi Mật Khẩu - FAT System";
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        StatusMessage = null;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            ErrorMessage = "Mật khẩu mới phải có tối thiểu 8 ký tự.";
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "Xác nhận mật khẩu mới không trùng khớp.";
            return;
        }

        if (_currentUserContext.User?.UserId is not int userId)
        {
            ErrorMessage = "Không xác định được phiên làm việc.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var success = await _authService.ChangePasswordAsync(userId, CurrentPassword, NewPassword);
            if (!success)
            {
                ErrorMessage = "Mật khẩu hiện tại không chính xác.";
                return;
            }

            StatusMessage = "Đổi mật khẩu thành công!";
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
        });
    }
}
