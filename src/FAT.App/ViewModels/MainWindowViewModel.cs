using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FAT.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ICurrentUserContext _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<NavigationItem> _allItems;

    public ObservableCollection<NavigationItem> MenuItems { get; } = [];
    [ObservableProperty] private ViewModelBase? _currentViewModel;
    [ObservableProperty] private NavigationItem? _selectedItem;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string? _loginError;
    public string UserDisplayName => _currentUser.User?.FullName ?? _currentUser.User?.Username ?? string.Empty;

    public MainWindowViewModel(INavigationService navigation, ICurrentUserContext currentUser,
        IServiceScopeFactory scopeFactory, IEnumerable<NavigationItem> items)
    {
        _navigation = navigation; _currentUser = currentUser; _scopeFactory = scopeFactory;
        _allItems = items.OrderBy(item => item.Order).ToList();
        _navigation.CurrentViewModelChanged += (_, _) => CurrentViewModel = _navigation.CurrentViewModel;
    }

    public async Task LoginAsync(string password)
    {
        LoginError = null;
        using var scope = _scopeFactory.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(Username, password);
        if (!result.IsSuccess || result.User is null) { LoginError = result.ErrorMessage; return; }

        _currentUser.SetUser(result.User);
        IsAuthenticated = true;
        OnPropertyChanged(nameof(UserDisplayName));
        RebuildMenu();
        SelectedItem = MenuItems.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is not null && IsAuthenticated) _ = _navigation.NavigateToAsync(value.ViewModelType);
    }

    [RelayCommand] private void Logout()
    {
        _navigation.ClearHistory(); _currentUser.Clear(); MenuItems.Clear();
        CurrentViewModel = null; SelectedItem = null; IsAuthenticated = false; LoginError = null;
        OnPropertyChanged(nameof(UserDisplayName));
    }

    [RelayCommand] private Task GoBackAsync() => _navigation.GoBackAsync();

    private void RebuildMenu()
    {
        MenuItems.Clear();
        foreach (var item in _allItems.Where(item => item.RequiresAdmin ? _currentUser.IsAdmin : !_currentUser.IsAdmin))
            MenuItems.Add(item);
    }
}
