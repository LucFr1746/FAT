using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Registration screen.
/// Pre-populates Google User Info when redirected from Google OAuth login.
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
    private string? _faculty = "Công nghệ Thông tin";

    [ObservableProperty]
    private int _majorId = 1;

    [ObservableProperty]
    private string? _phone = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _acceptTerms;

    [ObservableProperty]
    private string? _googleId;

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
            IsEmailReadOnly = true; // Lock Email field when coming from Google OAuth
        }
        else
        {
            IsEmailReadOnly = false;
        }

        return Task.CompletedTask;
    }

    [ObservableProperty]
    private bool _isAccountAlreadyExists;

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
                FullName: FullName.Trim(),
                Email: Email.Trim().ToLowerInvariant(),
                Faculty: Faculty,
                MajorId: MajorId,
                Phone: Phone,
                Password: Password,
                ConfirmPassword: ConfirmPassword,
                AcceptTerms: AcceptTerms,
                GoogleId: GoogleId,
                AvatarUrl: AvatarUrl
            );

            var result = await _authService.RegisterStudentAsync(dto);

            if (!result.IsSuccess || result.User == null)
            {
                var error = result.ErrorMessage ?? "Đăng ký không thành công. Vui lòng kiểm tra lại.";
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

            // Auto Login on successful registration
            _currentUserContext.SetUser(result.User);
            await _navigationService.NavigateToAsync<DashboardViewModel>();
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
            ErrorMessage = "Vui lòng nhập Mã số Sinh viên (VD: SE170000).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Vui lòng nhập Họ và tên đầy đủ.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập Email.";
            return false;
        }

        if (!Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            ErrorMessage = "Định dạng Email không hợp lệ.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Phone) && !Regex.IsMatch(Phone, @"^(0|\+84)[3|5|7|8|9][0-9]{8}$"))
        {
            ErrorMessage = "Số điện thoại không hợp lệ (Định dạng SĐT Việt Nam 10 chữ số).";
            return false;
        }

        // Password rules check (if password is entered)
        if (string.IsNullOrEmpty(GoogleId) || !string.IsNullOrWhiteSpace(Password))
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
            {
                ErrorMessage = "Mật khẩu tối thiểu phải từ 8 ký tự trở lên.";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[A-Z]"))
            {
                ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ cái viết hoa.";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[a-z]"))
            {
                ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ cái viết thường.";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[0-9]"))
            {
                ErrorMessage = "Mật khẩu phải chứa ít nhất 1 chữ số.";
                return false;
            }

            if (!Regex.IsMatch(Password, @"[\W_]"))
            {
                ErrorMessage = "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (!@#$%^&*...).";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu xác nhận không trùng khớp với mật khẩu đã nhập.";
                return false;
            }
        }

        if (!AcceptTerms)
        {
            ErrorMessage = "Bạn phải tích chọn đồng ý với Điều khoản Dịch vụ.";
            return false;
        }

        return true;
    }
}
