using CommunityToolkit.Mvvm.ComponentModel;
using FAT.App.Navigation;
using FAT.Services.Abstractions;

namespace FAT.App.ViewModels;

/// <summary>
/// Root ViewModel for MainWindow.
/// Manages top-level navigation container and session state.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public INavigationService NavigationService { get; }
    public ICurrentUserContext CurrentUserContext { get; }

    public MainWindowViewModel(INavigationService navigationService, ICurrentUserContext currentUserContext)
    {
        NavigationService = navigationService;
        CurrentUserContext = currentUserContext;

        CurrentUserContext.UserChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CurrentUserContext));
        };
    }
}
