using System.Windows;
using System.Windows.Controls;
using FAT.App.ViewModels.Auth;

namespace FAT.App.Views.Auth;

public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView()
    {
        InitializeComponent();
    }

    private void TxtCurrent_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox pb)
        {
            vm.CurrentPassword = pb.Password;
        }
    }

    private void TxtNew_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox pb)
        {
            vm.NewPassword = pb.Password;
        }
    }

    private void TxtConfirm_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox pb)
        {
            vm.ConfirmNewPassword = pb.Password;
        }
    }
}
