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
    private string? _statusMessage;

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
            Users = new ObservableCollection<UserDto>(list);
        });
    }

    [RelayCommand]
    private async Task ToggleUserStatusAsync(UserDto? user)
    {
        var target = user ?? SelectedUser;
        if (target == null) return;

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            var newStatus = !target.IsActive;
            await _userService.SetActiveAsync(target.UserId, newStatus);
            StatusMessage = $"Đã {(newStatus ? "Mở khóa" : "Khóa")} tài khoản '{target.Username}'.";
            await LoadUsersAsync();
        });
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserDto? user)
    {
        var target = user ?? SelectedUser;
        if (target == null) return;

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            const string defaultPassword = "User@123456";
            await _userService.ResetPasswordAsync(target.UserId, defaultPassword);
            StatusMessage = $"Đã đặt lại mật khẩu cho '{target.Username}' thành '{defaultPassword}'.";
        });
    }
}
