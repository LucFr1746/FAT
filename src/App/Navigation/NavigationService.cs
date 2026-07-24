using CommunityToolkit.Mvvm.ComponentModel;
using App.ViewModels;
using Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Navigation;

/// <summary>
/// Implementation of ViewModel-first navigation service for WPF.
/// Resolves ViewModels dynamically via IServiceProvider.
/// Enforces mandatory profile setup access control guard for students.
/// </summary>
public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly Stack<(Type ViewModelType, object? Parameter)> _history = new();

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public event EventHandler? CurrentViewModelChanged;

    public bool CanGoBack => _history.Count > 0;

    public NavigationService(IServiceProvider serviceProvider, ICurrentUserContext currentUserContext)
    {
        _serviceProvider = serviceProvider;
        _currentUserContext = currentUserContext;
    }

    public async Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        await NavigateToAsync(typeof(TViewModel), parameter);
    }

    public async Task NavigateToAsync(Type viewModelType, object? parameter = null)
    {
        // Access Control Guard: Incomplete student profile cannot access protected pages
        if (_currentUserContext.IsAuthenticated &&
            !_currentUserContext.IsAdmin &&
            _currentUserContext.User != null &&
            !_currentUserContext.User.IsProfileCompleted)
        {
            if (viewModelType != typeof(ViewModels.Auth.AcademicProfileSetupViewModel) &&
                viewModelType != typeof(ViewModels.Auth.LoginViewModel) &&
                viewModelType != typeof(ViewModels.Auth.RegisterViewModel))
            {
                viewModelType = typeof(ViewModels.Auth.AcademicProfileSetupViewModel);
            }
        }

        if (CurrentViewModel != null && CurrentViewModel.GetType() != viewModelType)
        {
            _history.Push((CurrentViewModel.GetType(), null));
        }

        var viewModel = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);

        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanGoBack));

        if (viewModel is INavigationAware navigationAware)
        {
            await navigationAware.OnNavigatedToAsync(parameter);
        }
    }

    public async Task GoBackAsync()
    {
        if (_history.Count > 0)
        {
            var (viewModelType, parameter) = _history.Pop();
            var viewModel = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);

            CurrentViewModel = viewModel;
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(CanGoBack));

            if (viewModel is INavigationAware navigationAware)
            {
                await navigationAware.OnNavigatedToAsync(parameter);
            }
        }
    }

    public void ClearHistory()
    {
        _history.Clear();
        OnPropertyChanged(nameof(CanGoBack));
    }
}
