using System.Windows;
using FAT.App.ViewModels;

namespace FAT.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void LoginClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            await viewModel.LoginAsync(LoginPassword.Password);
    }
}
