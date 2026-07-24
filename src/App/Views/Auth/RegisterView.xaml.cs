using System.Windows;
using System.Windows.Controls;
using App.ViewModels.Auth;

namespace App.Views.Auth;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm && sender is PasswordBox pb)
        {
            if (vm.Password != pb.Password)
            {
                vm.Password = pb.Password;
            }
        }
    }

    private void TxtPasswordPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm && sender is TextBox tb)
        {
            if (TxtPassword.Password != tb.Text)
            {
                TxtPassword.Password = tb.Text;
            }
        }
    }

    private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm && sender is PasswordBox pb)
        {
            if (vm.ConfirmPassword != pb.Password)
            {
                vm.ConfirmPassword = pb.Password;
            }
        }
    }

    private void TxtConfirmPasswordPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is RegisterViewModel vm && sender is TextBox tb)
        {
            if (TxtConfirmPassword.Password != tb.Text)
            {
                TxtConfirmPassword.Password = tb.Text;
            }
        }
    }
}
