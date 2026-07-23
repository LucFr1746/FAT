using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Helpers;
using FAT.App.Navigation;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Login screen.
/// Supports both Google OAuth login and Username/Password login.
/// Supports Show Password toggle.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    public LoginViewModel(
        IAuthService authService,
        IGoogleOAuthService googleOAuthService,
        ICurrentUserContext currentUserContext,
        INavigationService navigationService)
    {
        _authService = authService;
        _googleOAuthService = googleOAuthService;
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;
        Title = "Đăng Nhập System - FAT";

        // Load saved credentials if any
        var saved = CredentialStorage.LoadCredentials();
        if (saved != null)
        {
            Username = saved.Username;
            Password = saved.Password;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ Tên đăng nhập và Mật khẩu.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _authService.LoginAsync(Username, Password);
            if (!result.IsSuccess || result.User == null)
            {
                ErrorMessage = result.ErrorMessage ?? "Đăng nhập không thành công. Vui lòng kiểm tra lại thông tin.";
                return;
            }

            // Save credentials automatically
            CredentialStorage.SaveCredentials(Username, Password, isGoogleLogin: false);

            // Set session user
            _currentUserContext.SetUser(result.User);
            await _navigationService.NavigateToAsync<DashboardViewModel>();
        });
    }

    [RelayCommand]
    private async Task GoogleLoginAsync()
    {
        await RunBusyAsync(async () =>
        {
            ErrorMessage = null;
            var oauthResult = await _googleOAuthService.AuthenticateWithGoogleAsync();

            if (!oauthResult.IsSuccess || oauthResult.UserInfo == null)
            {
                ErrorMessage = oauthResult.ErrorMessage ?? "Đăng nhập Google thất bại hoặc bị hủy.";
                return;
            }

            // Attempt login with Google credentials
            var loginResult = await _authService.LoginWithGoogleAsync(oauthResult.UserInfo);

            if (loginResult.IsSuccess && loginResult.User != null)
            {
                CredentialStorage.SaveCredentials(oauthResult.UserInfo.Email, string.Empty, isGoogleLogin: true);
                _currentUserContext.SetUser(loginResult.User);
                await _navigationService.NavigateToAsync<DashboardViewModel>();
                return;
            }

            // Handle Account Not Found -> Auto navigate to Registration
            if (string.Equals(loginResult.ErrorMessage, "ACCOUNT_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Tài khoản Google chưa được đăng ký trong hệ thống. Đang chuyển sang màn hình đăng ký...";
                await Task.Delay(1000);
                await _navigationService.NavigateToAsync<RegisterViewModel>(oauthResult.UserInfo);
            }
            else
            {
                ErrorMessage = loginResult.ErrorMessage ?? "Đăng nhập Google thất bại.";
            }
        });
    }

    [RelayCommand]
    private async Task NavigateToRegisterAsync()
    {
        await _navigationService.NavigateToAsync<RegisterViewModel>();
    }
}
