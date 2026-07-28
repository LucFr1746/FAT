using System.Windows.Threading;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;

namespace App.ViewModels.Auth;

public partial class ForgotPasswordViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private DispatcherTimer? _countdownTimer;

    [ObservableProperty]
    private int _currentStep = 1; // 1: Input MSSV, 2: Input OTP, 3: Input New Password

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
    }

    [ObservableProperty]
    private string _mssvOrEmail = string.Empty;

    [ObservableProperty]
    private string _maskedEmail = string.Empty;

    [ObservableProperty]
    private string _otpCode = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _devOtpNotice;

    public bool HasDevOtpNotice => !string.IsNullOrWhiteSpace(DevOtpNotice);

    partial void OnDevOtpNoticeChanged(string? value) => OnPropertyChanged(nameof(HasDevOtpNotice));

    [ObservableProperty]
    private int _countdownSeconds = 60;

    [ObservableProperty]
    private bool _canResend = false;

    public string ResendButtonText => CanResend ? "Gửi lại mã OTP" : $"Gửi lại mã ({CountdownSeconds}s)";

    partial void OnCountdownSecondsChanged(int value) => OnPropertyChanged(nameof(ResendButtonText));

    partial void OnCanResendChanged(bool value) => OnPropertyChanged(nameof(ResendButtonText));

    public ForgotPasswordViewModel(IAuthService authService, INavigationService navigationService)
    {
        _authService = authService;
        _navigationService = navigationService;
        Title = "Quên Mật Khẩu - FAT System";
    }

    [RelayCommand]
    private async Task SendOtpAsync()
    {
        ErrorMessage = null;
        DevOtpNotice = null;

        if (string.IsNullOrWhiteSpace(MssvOrEmail))
        {
            ErrorMessage = "Vui lòng nhập Mã số sinh viên (MSSV) hoặc Email.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _authService.SendResetOtpAsync(MssvOrEmail);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Không thể gửi mã OTP. Vui lòng kiểm tra lại MSSV.";
                return;
            }

            MaskedEmail = result.MaskedEmail ?? MssvOrEmail;
            if (!string.IsNullOrWhiteSpace(result.DevOtpCode))
            {
                DevOtpNotice = $"💡 [Dev Mode Demo] Mã OTP của bạn là: {result.DevOtpCode}";
            }

            CurrentStep = 2;
            StartResendTimer();
        });
    }

    [RelayCommand]
    private async Task VerifyOtpAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(OtpCode) || OtpCode.Trim().Length != 6)
        {
            ErrorMessage = "Vui lòng nhập đầy đủ mã OTP gồm 6 chữ số.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var isValid = await _authService.VerifyResetOtpAsync(MssvOrEmail, OtpCode.Trim());
            if (!isValid)
            {
                ErrorMessage = "Mã OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại.";
                return;
            }

            CurrentStep = 3;
            StopResendTimer();
        });
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Vui lòng nhập mật khẩu mới.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Mật khẩu mới phải có tối thiểu 8 ký tự.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Mật khẩu xác nhận không trùng khớp với mật khẩu mới.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _authService.ResetPasswordWithOtpAsync(MssvOrEmail, OtpCode, NewPassword);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Không thể cập nhật mật khẩu mới.";
                return;
            }

            StopResendTimer();
            await _navigationService.NavigateToAsync<LoginViewModel>();
        });
    }

    [RelayCommand]
    private async Task ResendOtpAsync()
    {
        if (!CanResend)
        {
            return;
        }

        ErrorMessage = null;
        DevOtpNotice = null;

        await RunBusyAsync(async () =>
        {
            var result = await _authService.SendResetOtpAsync(MssvOrEmail);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Không thể gửi lại mã OTP.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.DevOtpCode))
            {
                DevOtpNotice = $"💡 [Dev Mode Demo] Mã OTP mới của bạn là: {result.DevOtpCode}";
            }

            StartResendTimer();
        });
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        StopResendTimer();
        await _navigationService.NavigateToAsync<LoginViewModel>();
    }

    private void StartResendTimer()
    {
        StopResendTimer();
        CountdownSeconds = 60;
        CanResend = false;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, e) =>
        {
            if (CountdownSeconds > 1)
            {
                CountdownSeconds--;
            }
            else
            {
                CountdownSeconds = 0;
                CanResend = true;
                StopResendTimer();
            }
        };
        _countdownTimer.Start();
    }

    private void StopResendTimer()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }
}
