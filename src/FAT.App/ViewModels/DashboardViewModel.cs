using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.App.ViewModels.Auth;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.ViewModels;

/// <summary>
/// Root Shell Dashboard ViewModel shown after successful login.
/// Displays Top Navigation, User Badge (Name, Avatar, Role Student/Admin)
/// and handles tab navigation & Logout.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, INavigationAware
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    public ICurrentUserContext CurrentUserContext { get; }

    [ObservableProperty]
    private string _activeTab = "Home";

    [ObservableProperty]
    private ViewModelBase? _currentTabViewModel;

    [ObservableProperty]
    private CurrentUserInfo? _currentUser;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isProfileMenuOpen;

    public DashboardViewModel(
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        ICurrentUserContext currentUserContext)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        CurrentUserContext = currentUserContext;
        Title = "FAT System - FPT Academic & Conduct Tracker";

        CurrentUserContext.UserChanged += (s, e) =>
        {
            CurrentUser = CurrentUserContext.User;
            IsAdmin = CurrentUserContext.IsAdmin;
        };
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        CurrentUser = CurrentUserContext.User;
        IsAdmin = CurrentUserContext.IsAdmin;
        SwitchTab("Home");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleProfileMenu()
    {
        IsProfileMenuOpen = !IsProfileMenuOpen;
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        ActiveTab = tabName;
        IsProfileMenuOpen = false; // Close profile menu dropdown on tab switch

        switch (tabName)
        {
            case "Profile":
                var profileVm = _serviceProvider.GetRequiredService<ProfileViewModel>();
                CurrentTabViewModel = profileVm;
                _ = profileVm.OnNavigatedToAsync(null);
                break;

            case "ChangePassword":
                CurrentTabViewModel = _serviceProvider.GetRequiredService<ChangePasswordViewModel>();
                break;

            case "UserManagement":
                var userMgmtVm = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                CurrentTabViewModel = userMgmtVm;
                _ = userMgmtVm.OnNavigatedToAsync(null);
                break;

            case "Home":
            default:
                CurrentTabViewModel = null; // Show Home Dashboard Cards
                break;
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
