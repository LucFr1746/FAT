using System.Windows;
using System.Windows.Controls;
using App.ViewModels.Auth;

namespace App.Views.Auth;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
        {
            if (vm.Password != pb.Password)
            {
                vm.Password = pb.Password;
            }
        }
    }

    private void TxtPasswordPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is TextBox tb)
        {
            if (TxtPassword.Password != tb.Text)
            {
                TxtPassword.Password = tb.Text;
            }
        }
    }
}
