using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Auth;

/// <summary>
/// ViewModel for User Account Management (Admin feature).
/// </summary>
public partial class UserManagementViewModel : ViewModelBase, INavigationAware
{
    private readonly IUserService _userService;
    public ICurrentUserContext CurrentUserContext { get; }

    [ObservableProperty]
    private string? _searchKeyword;

    [ObservableProperty]
    private ObservableCollection<UserDto> _users = new();

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private bool _isResetModalOpen;

    [ObservableProperty]
    private UserDto? _resetTargetUser;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private string? _modalErrorMessage;

    public bool HasModalError => !string.IsNullOrWhiteSpace(ModalErrorMessage);

    partial void OnModalErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasModalError));

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public UserManagementViewModel(IUserService userService, ICurrentUserContext currentUserContext)
    {
        _userService = userService;
        CurrentUserContext = currentUserContext;
        Title = "Quản Lý Người Dùng - FAT System";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        await LoadUsersAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task LoadUsersAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            var list = await _userService.GetUsersAsync(SearchKeyword, cancellationToken);
            Users.Clear();
            foreach (var item in list)
            {
                Users.Add(item);
            }
        });
    }

    [RelayCommand]
    private async Task ToggleUserStatusAsync(UserDto? user)
    {
        var target = user ?? SelectedUser;
        if (target == null)
        {
            return;
        }

        if (CurrentUserContext.User != null && target.UserId == CurrentUserContext.User.UserId)
        {
            StatusMessage = null;
            ErrorMessage = "Bạn không thể tự khóa tài khoản Admin đang đăng nhập.";
            return;
        }

        var actionText = target.IsActive ? "khóa" : "mở khóa";
        var confirmResult = System.Windows.MessageBox.Show(
            $"Bạn có chắc chắn muốn {actionText} tài khoản '{target.Username}' không?",
            "Xác nhận thay đổi trạng thái",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmResult != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            ErrorMessage = null;
            var newStatus = !target.IsActive;
            await _userService.SetActiveAsync(target.UserId, newStatus);

            var index = Users.IndexOf(target);
            var updatedUser = target with { IsActive = newStatus };
            if (index >= 0)
            {
                Users[index] = updatedUser;
            }

            StatusMessage = $"Đã {(newStatus ? "Mở khóa" : "Khóa")} tài khoản '{target.Username}' thành công.";
        });
    }

    [RelayCommand]
    private void OpenResetPasswordModal(UserDto? user)
    {
        var target = user ?? SelectedUser;
        if (target == null)
        {
            return;
        }

        ResetTargetUser = target;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        IsPasswordVisible = false;
        ModalErrorMessage = null;
        IsResetModalOpen = true;
    }

    [RelayCommand]
    private void CloseResetPasswordModal()
    {
        IsResetModalOpen = false;
        ResetTargetUser = null;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        ModalErrorMessage = null;
    }

    [RelayCommand]
    private async Task ConfirmResetPasswordAsync()
    {
        ModalErrorMessage = null;

        if (ResetTargetUser == null)
        {
            ModalErrorMessage = "* Không tìm thấy tài khoản cần đặt lại mật khẩu.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ModalErrorMessage = "* Vui lòng nhập mật khẩu mới.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ModalErrorMessage = "* Mật khẩu mới phải có tối thiểu 8 ký tự.";
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            ModalErrorMessage = "* Mật khẩu mới và xác nhận mật khẩu không trùng khớp.";
            return;
        }

        var confirmResult = System.Windows.MessageBox.Show(
            $"Bạn có chắc chắn muốn lưu mật khẩu mới cho tài khoản '{ResetTargetUser.Username}' ({ResetTargetUser.FullName}) vào cơ sở dữ liệu không?",
            "Xác Nhận Cập Nhật Mật Khẩu",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmResult != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            ErrorMessage = null;
            await _userService.ResetPasswordAsync(ResetTargetUser.UserId, NewPassword);
            StatusMessage = $"Đã cập nhật mật khẩu mới cho tài khoản '{ResetTargetUser.Username}' thành công vào cơ sở dữ liệu.";
            IsResetModalOpen = false;
        });
    }
}
