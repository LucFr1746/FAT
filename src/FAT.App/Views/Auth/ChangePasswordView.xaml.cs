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
            if (vm.CurrentPassword != pb.Password)
            {
                vm.CurrentPassword = pb.Password;
            }
        }
    }

    private void TxtCurrentPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is TextBox tb)
        {
            if (TxtCurrent.Password != tb.Text)
            {
                TxtCurrent.Password = tb.Text;
            }
        }
    }

    private void TxtNew_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox pb)
        {
            if (vm.NewPassword != pb.Password)
            {
                vm.NewPassword = pb.Password;
            }
        }
    }

    private void TxtNewPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is TextBox tb)
        {
            if (TxtNew.Password != tb.Text)
            {
                TxtNew.Password = tb.Text;
            }
        }
    }

    private void TxtConfirm_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox pb)
        {
            if (vm.ConfirmNewPassword != pb.Password)
            {
                vm.ConfirmNewPassword = pb.Password;
            }
        }
    }

    private void TxtConfirmPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is TextBox tb)
        {
            if (TxtConfirm.Password != tb.Text)
            {
                TxtConfirm.Password = tb.Text;
            }
        }
    }
}
