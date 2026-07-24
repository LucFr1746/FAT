using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Auth;

/// <summary>
/// ViewModel for the First Login Academic Profile Setup screen.
/// Prompts first-time users to select their Major and Current Semester.
/// </summary>
public partial class AcademicProfileSetupViewModel : ViewModelBase, INavigationAware
{
    private readonly IUserService _userService;
    private readonly ICourseService _courseService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _studentCode = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MajorDto> _majors = new();

    [ObservableProperty]
    private MajorDto? _selectedMajor;

    [ObservableProperty]
    private ObservableCollection<int> _termNumbers = new();

    [ObservableProperty]
    private int _selectedTermNo = 1;

    public AcademicProfileSetupViewModel(
        IUserService userService,
        ICourseService courseService,
        ICurrentUserContext currentUserContext,
        INavigationService navigationService)
    {
        _userService = userService;
        _courseService = courseService;
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;
        Title = "Hoàn Tất Hồ Sơ Học Tập - FAT";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            var user = _currentUserContext.User;
            if (user != null)
            {
                StudentCode = user.StudentCode ?? user.Username;
                FullName = user.FullName ?? string.Empty;
            }

            var majors = await _courseService.GetMajorsAsync(cancellationToken);
            Majors.Clear();
            foreach (var major in majors)
            {
                Majors.Add(major);
            }

            SelectedMajor = Majors.FirstOrDefault();

            TermNumbers.Clear();
            for (int i = 0; i <= 9; i++)
            {
                TermNumbers.Add(i);
            }
            SelectedTermNo = 1;
        });
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        ErrorMessage = null;

        if (SelectedMajor == null)
        {
            ErrorMessage = "* Vui lòng chọn Ngành học của bạn.";
            return;
        }

        if (_currentUserContext.StudentId is not int studentId)
        {
            ErrorMessage = "* Không tìm thấy thông tin tài khoản sinh viên.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _userService.CompleteAcademicProfileAsync(studentId, SelectedMajor.MajorId, SelectedTermNo);

            // Update session context with completed profile flag
            var current = _currentUserContext.User;
            if (current != null)
            {
                _currentUserContext.SetUser(current with { IsProfileCompleted = true });
            }

            // Proceed to main application dashboard
            await _navigationService.NavigateToAsync<DashboardViewModel>();
        });
    }
}
