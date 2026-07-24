using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Registration screen.
/// Formats error messages as * Validate, provides inline password requirements,
/// supports Show Password toggle, and provides Major selection dropdown.
/// </summary>
public partial class RegisterViewModel : ViewModelBase, INavigationAware
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private bool _isEmailReadOnly;

    [ObservableProperty]
    private string _studentCode = string.Empty;

    [ObservableProperty]
    private List<string> _majorOptions = new()
    {
        "Software Engineering"
    };

    [ObservableProperty]
    private string _selectedMajor = "Software Engineering";

    [ObservableProperty]
    private int _majorId = 1;

    [ObservableProperty]
    private string? _phone = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _acceptTerms;

    [ObservableProperty]
    private string? _googleId;

    [ObservableProperty]
    private bool _isAccountAlreadyExists;

    public RegisterViewModel(
        IAuthService authService,
        ICurrentUserContext currentUserContext,
        INavigationService navigationService)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;
        Title = "Đăng Ký Tài Khoản Sinh Viên - FAT";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        if (parameter is GoogleUserInfoDto googleUser)
        {
            GoogleId = googleUser.GoogleId;
            Email = googleUser.Email;
            FullName = googleUser.FullName;
            AvatarUrl = googleUser.PictureUrl;
            IsEmailReadOnly = true;
        }
        else
        {
            IsEmailReadOnly = false;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        IsAccountAlreadyExists = false;
        if (!ValidateForm())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var dto = new RegisterRequestDto(
                StudentCode: StudentCode.Trim().ToUpperInvariant(),
                Password: Password,
                ConfirmPassword: ConfirmPassword,
                AcceptTerms: AcceptTerms,
                FullName: FullName?.Trim(),
                Email: Email?.Trim(),
                GoogleId: GoogleId,
                AvatarUrl: AvatarUrl
            );

            var result = await _authService.RegisterStudentAsync(dto);

            if (!result.IsSuccess || result.User == null)
            {
                var error = result.ErrorMessage ?? "Đăng ký không thành công. Vui lòng kiểm tra lại.";
                if (!error.StartsWith("*"))
                {
                    error = "* " + error;
                }

                if (error.Contains("đã tồn tại") || error.Contains("đã được đăng ký"))
                {
                    IsAccountAlreadyExists = true;
                    ErrorMessage = $"{error} Vui lòng quay lại màn hình Đăng nhập để truy cập.";
                }
                else
                {
                    ErrorMessage = error;
                }
                return;
            }

            _currentUserContext.SetUser(result.User);
            if (!result.User.IsAdmin && !result.User.IsProfileCompleted)
            {
                await _navigationService.NavigateToAsync<AcademicProfileSetupViewModel>();
            }
            else
            {
                await _navigationService.NavigateToAsync<DashboardViewModel>();
            }
        });
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await _navigationService.NavigateToAsync<LoginViewModel>();
    }

    private bool ValidateForm()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(StudentCode))
        {
            ErrorMessage = "* Vui lòng nhập Mã số Sinh viên (VD: SE170000).";
            return false;
        }

        // Password rules check
        if (string.IsNullOrEmpty(GoogleId) || !string.IsNullOrWhiteSpace(Password))
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
            {
                ErrorMessage = "* Mật khẩu phải có tối thiểu 8 ký tự.";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[A-Z]"))
            {
                ErrorMessage = "* Mật khẩu phải chứa ít nhất 1 chữ cái viết hoa (A-Z).";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[\W_]"))
            {
                ErrorMessage = "* Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (!@#$%^&*...).";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "* Mật khẩu xác nhận không trùng khớp với mật khẩu đã nhập.";
                return false;
            }
        }

        if (!AcceptTerms)
        {
            ErrorMessage = "* Bạn phải tích chọn đồng ý với Điều khoản sử dụng.";
            return false;
        }

        return true;
    }
}
