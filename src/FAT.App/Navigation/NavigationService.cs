using CommunityToolkit.Mvvm.ComponentModel;
using FAT.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.Navigation;

/// <summary>
/// Implementation of ViewModel-first navigation service for WPF.
/// Resolves ViewModels dynamically via IServiceProvider.
/// </summary>
public partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<(Type ViewModelType, object? Parameter)> _history = new();

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public event EventHandler? CurrentViewModelChanged;

    public bool CanGoBack => _history.Count > 0;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
    {
        await NavigateToAsync(typeof(TViewModel), parameter);
    }

    public async Task NavigateToAsync(Type viewModelType, object? parameter = null)
    {
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
