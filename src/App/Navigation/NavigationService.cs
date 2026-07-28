using App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Services.Abstractions;

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

    /// <summary>
    /// The DI scope owning the screen currently on display.
    ///
    /// WITHOUT THIS THE WHOLE APPLICATION SHARES ONE FAT_DBContext. This class
    /// is a singleton, so resolving a view model straight out of
    /// <see cref="_serviceProvider"/> resolves it - and every scoped service it
    /// depends on - from the ROOT scope, which lives as long as the process.
    /// One change tracker then serves every screen: an entity another screen
    /// loaded is handed back from the cache instead of being read again, so an
    /// edit made on one screen does not show up on the next one, and a
    /// SaveChanges that fails leaves its half-applied entity staged for
    /// whichever screen saves next.
    /// </summary>
    private IServiceScope? _currentScope;

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

        var viewModel = ActivateInNewScope(viewModelType);

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
            var viewModel = ActivateInNewScope(viewModelType);

            CurrentViewModel = viewModel;
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(CanGoBack));

            if (viewModel is INavigationAware navigationAware)
            {
                await navigationAware.OnNavigatedToAsync(parameter);
            }
        }
    }

    /// <summary>
    /// Resolves a view model inside a FRESH scope, then releases the scope the
    /// previous screen was using.
    ///
    /// The old scope is disposed only AFTER the new one has resolved
    /// successfully: a view model whose constructor throws must leave the
    /// current screen working rather than tear down its services underneath it.
    /// </summary>
    private ViewModelBase ActivateInNewScope(Type viewModelType)
    {
        var scope = _serviceProvider.CreateScope();

        ViewModelBase viewModel;
        try
        {
            viewModel = (ViewModelBase)scope.ServiceProvider.GetRequiredService(viewModelType);
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        var outgoing = CurrentViewModel;
        var previousScope = _currentScope;
        _currentScope = scope;

        // The outgoing instance is never reused - GoBackAsync re-resolves from
        // the type - so it is safe to dispose. DashboardViewModel needs it: it
        // owns a second scope for whichever tab was open, which the scope
        // disposal below cannot reach.
        (outgoing as IDisposable)?.Dispose();
        previousScope?.Dispose();

        return viewModel;
    }

    public void ClearHistory()
    {
        _history.Clear();
        OnPropertyChanged(nameof(CanGoBack));
    }
}
