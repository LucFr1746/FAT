using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Auth;

/// <summary>
/// ViewModel for the Student Profile screen.
/// Supports Edit Mode toggling and dropdown selection for Majors & Semesters.
/// </summary>
public partial class ProfileViewModel : ViewModelBase, INavigationAware
{
    private readonly IUserService _userService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private StudentProfileDto? _profile;

    [ObservableProperty]
    private string _studentCode = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string? _email = string.Empty;

    [ObservableProperty]
    private DateTime? _dateOfBirth;

    [ObservableProperty]
    private string _selectedSemester = "Kỳ 5";

    [ObservableProperty]
    private string _selectedMajor = "Software Engineering";

    [ObservableProperty]
    private string _selectedCampus = "Hồ Chí Minh";

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isGoogleLinked;

    public bool IsEmailReadOnly => IsGoogleLinked || !IsEditMode;

    partial void OnIsEditModeChanged(bool value) => OnPropertyChanged(nameof(IsEmailReadOnly));
    partial void OnIsGoogleLinkedChanged(bool value) => OnPropertyChanged(nameof(IsEmailReadOnly));

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public List<string> Semesters { get; } = new()
    {
        "Kỳ 1",
        "Kỳ 2",
        "Kỳ 3",
        "Kỳ 4",
        "Kỳ 5",
        "Kỳ 6",
        "Kỳ 7",
        "Kỳ 8",
        "Kỳ 9"
    };

    public List<string> Majors { get; } = new()
    {
        "Software Engineering"
    };

    public List<string> Campuses { get; } = new()
    {
        "Hồ Chí Minh",
        "Hà Nội",
        "Quy Nhơn",
        "Đà Nẵng",
        "Cần Thơ"
    };

    public ProfileViewModel(IUserService userService, ICurrentUserContext currentUserContext, INavigationService navigationService)
    {
        _userService = userService;
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;
        Title = "Hồ Sơ Cá Nhân - FAT System";
    }

    [RelayCommand]
    private async Task NavigateToHomeAsync()
    {
        await _navigationService.NavigateToAsync<DashboardViewModel>();
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        IsEditMode = false;
        await LoadProfileAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task LoadProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUserContext.IsAdmin)
        {
            ErrorMessage = "Tài khoản Quản trị viên (Admin) không sử dụng hồ sơ sinh viên.";
            return;
        }

        if (_currentUserContext.StudentId is not int studentId)
        {
            ErrorMessage = "Không tìm thấy hồ sơ sinh viên tương ứng với tài khoản này.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            Profile = await _userService.GetProfileAsync(studentId, cancellationToken);
            if (Profile != null)
            {
                StudentCode = Profile.StudentCode;
                FullName = Profile.FullName;
                Email = Profile.Email;
                DateOfBirth = Profile.DateOfBirth ?? new DateTime(2003, 1, 1);
                SelectedMajor = string.IsNullOrWhiteSpace(Profile.MajorName) ? "Software Engineering" : Profile.MajorName;
                Username = Profile.Username;
                SelectedSemester = Profile.CurrentSemester ?? "Kỳ 5";
                SelectedCampus = string.IsNullOrWhiteSpace(Profile.Campus) ? "Hồ Chí Minh" : Profile.Campus;
                IsGoogleLinked = Profile.IsGoogleLinked;
            }
        });
    }

    [RelayCommand]
    private void EnableEditMode()
    {
        IsEditMode = true;
        StatusMessage = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task CancelEditAsync()
    {
        IsEditMode = false;
        StatusMessage = null;
        ErrorMessage = null;
        await LoadProfileAsync();
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (_currentUserContext.StudentId is not int studentId)
        {
            ErrorMessage = "Không tìm thấy hồ sơ sinh viên.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            ErrorMessage = null;
            await _userService.UpdateProfileAsync(studentId, FullName, Email, DateOfBirth, SelectedSemester, SelectedMajor, SelectedCampus);
            IsEditMode = false; // Switch back to "Chỉnh sửa hồ sơ" mode
            StatusMessage = "Lưu hồ sơ thành công";
        });
    }
}
