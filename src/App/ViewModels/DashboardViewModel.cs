using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using App.ViewModels.Auth;
using App.ViewModels.Catalog;
using App.ViewModels.Student;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.Extensions.DependencyInjection;

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
    private string _currentSemesterLabel = "Đang tải...";

    [ObservableProperty]
    private int _totalSubjectCount;

    public DashboardViewModel(
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext,
        ICourseService courseService,
        ICatalogAdminService catalogAdminService)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        CurrentUserContext = currentUserContext;
        _courseService = courseService;
        _catalogAdminService = catalogAdminService;
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
    }

    [RelayCommand]
    private void ToggleCatalogMenu()
    {
        IsCatalogMenuOpen = !IsCatalogMenuOpen;
        IsProfileMenuOpen = false;
    }

    [RelayCommand]
    private async Task SwitchTabAsync(string tabName)
    {
        ActiveTab = tabName;
        IsProfileMenuOpen = false; // Close profile menu dropdown on tab switch
        IsCatalogMenuOpen = false; // Close catalog dropdown on tab switch

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

                case "Home":
                default:
                    CurrentTabViewModel = null; // Show Home Dashboard Cards
                    if (IsAdmin)
                    {
                        await LoadAdminSummaryAsync();
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
