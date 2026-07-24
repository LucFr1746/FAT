using App.ViewModels;

namespace App.Navigation;

/// <summary>
/// Moves between screens. FROZEN CONTRACT - owner: Member 1.
///
/// WHY NOT Frame / NavigationWindow:
/// Frame is view-first - it navigates to a XAML file by URI, which makes it
/// awkward to resolve the view model from the DI container and awkward to pass
/// arguments. This design is view-model-first: NavigateToAsync&lt;TViewModel&gt;()
/// resolves the view model from the container and a DataTemplate supplies the
/// view. Navigation stays type-safe and view models arrive fully injected.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>Used when the target type is only known at runtime, e.g. a menu click.</summary>
    Task NavigateToAsync(Type viewModelType, object? parameter = null);

    bool CanGoBack { get; }
    Task GoBackAsync();

    /// <summary>Clears the navigation history. Call this on sign-out.</summary>
    void ClearHistory();
}
