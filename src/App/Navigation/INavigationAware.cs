namespace App.Navigation;

/// <summary>
/// Implemented by view models that need to load data when navigated to.
///
/// Loading happens here rather than in the constructor because a constructor
/// cannot await, which forces either .Result or async void - both of which
/// freeze the UI thread.
/// </summary>
public interface INavigationAware
{
    /// <param name="parameter">Argument passed by the navigation call; may be null.</param>
    Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default);
}
