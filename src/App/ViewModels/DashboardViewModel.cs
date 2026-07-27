using App.Navigation;
using App.ViewModels.Auth;
using App.ViewModels.Catalog;
using App.ViewModels.Grades;
using App.ViewModels.Materials;
using App.ViewModels.Student;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels;

/// <summary>
/// Root Shell Dashboard ViewModel shown after successful login.
/// Displays Top Navigation, User Badge (Name, Avatar, Role Student/Admin)
/// and handles tab navigation & Logout.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly ICourseService _courseService;
    private readonly ICatalogAdminService _catalogAdminService;
    private readonly IStudentCurriculumService _studentCurriculumService;
    public ICurrentUserContext CurrentUserContext { get; }

    [ObservableProperty]
    private string _activeTab = "Home";

    [ObservableProperty]
    private ViewModelBase? _currentTabViewModel;

    public bool IsHomeTab => CurrentTabViewModel is null;

    partial void OnCurrentTabViewModelChanged(ViewModelBase? value)
    {
        OnPropertyChanged(nameof(IsHomeTab));
    }

    [ObservableProperty]
    private CurrentUserInfo? _currentUser;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isStudent;

    [ObservableProperty]
    private bool _isProfileMenuOpen;

    [ObservableProperty]
    private bool _isCatalogMenuOpen;

    [ObservableProperty]
    private bool _isGradeMenuOpen;

    [ObservableProperty]
    private string _currentSemesterLabel = "Đang tải...";

    [ObservableProperty]
    private int _totalSubjectCount;

    [ObservableProperty]
    private StudentTermCurriculumDto? _currentTermCurriculum;

    [ObservableProperty]
    private string _studentCurrentTermTitle = "Kỳ Học Hiện Tại";

    [ObservableProperty]
    private string _termGpaDisplay = "--";

    [ObservableProperty]
    private string _termCreditsDisplay = "0 Tín chỉ";

    public DashboardViewModel(
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext,
        ICourseService courseService,
        ICatalogAdminService catalogAdminService,
        IStudentCurriculumService studentCurriculumService)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        CurrentUserContext = currentUserContext;
        _courseService = courseService;
        _catalogAdminService = catalogAdminService;
        _studentCurriculumService = studentCurriculumService;
        Title = "FAT System - FPT Academic & Conduct Tracker";

        CurrentUserContext.UserChanged += (s, e) =>
        {
            CurrentUser = CurrentUserContext.User;
            IsAdmin = CurrentUserContext.IsAdmin;
            IsStudent = CurrentUserContext.IsAuthenticated && !CurrentUserContext.IsAdmin;
        };
    }


    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        CurrentUser = CurrentUserContext.User;
        IsAdmin = CurrentUserContext.IsAdmin;
        IsStudent = CurrentUserContext.IsAuthenticated && !CurrentUserContext.IsAdmin;

        if (IsStudent && CurrentUser != null && !CurrentUser.IsProfileCompleted)
        {
            await _navigationService.NavigateToAsync<AcademicProfileSetupViewModel>();
            return;
        }

        await SwitchTabAsync("Home");
    }

    [RelayCommand]
    private void ToggleProfileMenu()
    {
        IsProfileMenuOpen = !IsProfileMenuOpen;
        IsCatalogMenuOpen = false;
        IsGradeMenuOpen = false;
    }

    [RelayCommand]
    private void ToggleCatalogMenu()
    {
        IsCatalogMenuOpen = !IsCatalogMenuOpen;
        IsProfileMenuOpen = false;
        IsGradeMenuOpen = false;
    }

    [RelayCommand]
    private void ToggleGradeMenu()
    {
        IsGradeMenuOpen = !IsGradeMenuOpen;
        IsProfileMenuOpen = false;
        IsCatalogMenuOpen = false;
    }

    [RelayCommand]
    private async Task SwitchTabAsync(string tabName)
    {
        ActiveTab = tabName;
        IsProfileMenuOpen = false; // Close profile menu dropdown on tab switch
        IsCatalogMenuOpen = false; // Close catalog dropdown on tab switch
        IsGradeMenuOpen = false;

        try
        {
            switch (tabName)
            {
                case "Profile":
                    if (IsAdmin)
                    {
                        break; // Admin does not view/edit student profile
                    }

                    var profileVm = _serviceProvider.GetRequiredService<ProfileViewModel>();
                    CurrentTabViewModel = profileVm;
                    await profileVm.OnNavigatedToAsync(null);
                    break;

                case "ChangePassword":
                    CurrentTabViewModel = _serviceProvider.GetRequiredService<ChangePasswordViewModel>();
                    break;

                case "UserManagement":
                    var userMgmtVm = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    CurrentTabViewModel = userMgmtVm;
                    await userMgmtVm.OnNavigatedToAsync(null);
                    break;

                // ----- Catalog administration -----
                // Guarded here as well as in the XAML: hiding a button is not
                // authorization, and every service behind these screens
                // re-checks IsAdmin for itself.
                case "MajorAdmin":
                    if (!IsAdmin)
                    {
                        break;
                    }

                    await ShowTabAsync<MajorAdminViewModel>();
                    break;

                case "SemesterAdmin":
                    if (!IsAdmin)
                    {
                        break;
                    }

                    await ShowTabAsync<SemesterAdminViewModel>();
                    break;

                case "SubjectAdmin":
                    if (!IsAdmin)
                    {
                        break;
                    }

                    await ShowTabAsync<SubjectAdminViewModel>();
                    break;

                case "CurriculumAdmin":
                    if (!IsAdmin)
                    {
                        break;
                    }

                    await ShowTabAsync<CurriculumAdminViewModel>();
                    break;

                case "FlmImport":
                    if (!IsAdmin)
                    {
                        break;
                    }

                    await ShowTabAsync<FlmImportViewModel>();
                    break;

                // ----- Materials library (everyone) -----
                case "MaterialLibrary":
                    await ShowTabAsync<MaterialLibraryViewModel>();
                    break;

                // ----- Student screens -----
                case "MyCurriculum":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<MyCurriculumViewModel>();
                    break;

                case "GpaPrediction":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<GpaPredictionViewModel>();
                    break;

                case "ViewGrades":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<GradeListViewModel>();
                    break;

                case "ManageGrades":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<GradeEntryViewModel>();
                    break;

                case "GpaCalculator":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<GpaCalculatorViewModel>();
                    break;

                case "Transcript":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<TranscriptViewModel>();
                    break;

                case "Statistics":
                    if (!IsStudent)
                    {
                        break;
                    }

                    await ShowTabAsync<StatisticsViewModel>();
                    break;

                case "Home":
                default:
                    CurrentTabViewModel = null; // Show Home Dashboard Cards
                    if (IsAdmin)
                    {
                        await LoadAdminSummaryAsync();
                    }
                    else if (IsStudent && CurrentUser?.StudentId is int studentId)
                    {
                        await LoadStudentHomeDataAsync(studentId);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error switching tab to {tabName}: {ex}");
        }
    }

    /// <summary>
    /// Loads the current term curriculum subjects, grades, and GPA for the student's Home Dashboard.
    /// </summary>
    private async Task LoadStudentHomeDataAsync(int studentId)
    {
        try
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            var profile = await userService.GetProfileAsync(studentId);
            int termNo = profile?.CurrentTermNo ?? 1;
            if (termNo < 1 || termNo > 9) termNo = 1;

            StudentCurrentTermTitle = $"Kỳ {termNo}";

            var termCurriculum = await _studentCurriculumService.GetTermCurriculumAsync(studentId, termNo);
            CurrentTermCurriculum = termCurriculum;

            if (termCurriculum != null)
            {
                TermCreditsDisplay = $"{termCurriculum.TermCredits} Tín chỉ ({termCurriculum.Subjects.Count} môn)";

                var gradedSubjects = termCurriculum.Subjects
                    .Where(s => s.CountsTowardGpa && s.MyFinalScore.HasValue)
                    .ToList();

                if (gradedSubjects.Any())
                {
                    decimal totalWeighted = gradedSubjects.Sum(s => s.MyFinalScore!.Value * s.Credits);
                    int totalCreds = gradedSubjects.Sum(s => s.Credits);
                    decimal termGpa = totalCreds > 0 ? totalWeighted / totalCreds : 0m;
                    TermGpaDisplay = $"{termGpa:F2} / 10";
                }
                else
                {
                    TermGpaDisplay = "Chưa có điểm";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading student home data: {ex}");
            StudentCurrentTermTitle = "Kỳ 1";
            TermGpaDisplay = "--";
        }
    }


    /// <summary>
    /// Populates the Home dashboard's KPI cards. Best-effort: a failure here
    /// should not block the admin from reaching the rest of the shell, so it
    /// only leaves the labels at their default text.
    /// </summary>
    private async Task LoadAdminSummaryAsync()
    {
        try
        {
            var currentSemester = await _courseService.GetCurrentSemesterAsync();
            CurrentSemesterLabel = currentSemester is null
                ? "Chưa thiết lập"
                : $"{currentSemester.SemesterName} - Đang diễn ra";

            var subjects = await _catalogAdminService.GetCoursesAsync(new CourseFilter());
            TotalSubjectCount = subjects.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading admin summary: {ex}");
        }
    }

    /// <summary>
    /// Resolves a tab's view model, shows it and lets it load.
    ///
    /// Written once rather than repeated per case: the step easily forgotten is
    /// the OnNavigatedToAsync call, and a screen that never loads shows an empty
    /// grid with no error to explain it.
    /// </summary>
    private async Task ShowTabAsync<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        CurrentTabViewModel = viewModel;

        if (viewModel is INavigationAware navigationAware)
        {
            await navigationAware.OnNavigatedToAsync(null);
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsProfileMenuOpen = false;
        CurrentUserContext.Clear();
        _navigationService.ClearHistory();
        await _navigationService.NavigateToAsync<LoginViewModel>();
    }
}
