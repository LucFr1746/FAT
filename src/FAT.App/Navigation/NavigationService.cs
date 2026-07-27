using FAT.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.Navigation;

public sealed class NavigationService(IServiceScopeFactory scopeFactory) : INavigationService, IDisposable
{
    private readonly Stack<(ViewModelBase ViewModel, IServiceScope Scope)> _history = [];
    private IServiceScope? _currentScope;

    public ViewModelBase? CurrentViewModel { get; private set; }
    public event EventHandler? CurrentViewModelChanged;
    public bool CanGoBack => _history.Count > 0;

    public Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase =>
        NavigateToAsync(typeof(TViewModel), parameter);

    public async Task NavigateToAsync(Type viewModelType, object? parameter = null)
    {
        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
            throw new ArgumentException("Navigation targets must derive from ViewModelBase.", nameof(viewModelType));

        var scope = scopeFactory.CreateScope();
        ViewModelBase next;
        try { next = (ViewModelBase)scope.ServiceProvider.GetRequiredService(viewModelType); }
        catch { scope.Dispose(); throw; }

        if (CurrentViewModel is not null && _currentScope is not null)
            _history.Push((CurrentViewModel, _currentScope));

        CurrentViewModel = next;
        _currentScope = scope;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);

        if (next is INavigationAware aware)
            await aware.OnNavigatedToAsync(parameter);
    }

    public Task GoBackAsync()
    {
        if (_history.Count == 0) return Task.CompletedTask;
        _currentScope?.Dispose();
        var previous = _history.Pop();
        CurrentViewModel = previous.ViewModel;
        _currentScope = previous.Scope;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void ClearHistory()
    {
        _currentScope?.Dispose();
        _currentScope = null;
        while (_history.TryPop(out var entry)) entry.Scope.Dispose();
        CurrentViewModel = null;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => ClearHistory();
}
