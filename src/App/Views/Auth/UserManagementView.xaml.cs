using System.Windows;
using System.Windows.Controls;
using App.ViewModels.Auth;

namespace App.Views.Auth;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    private void TxtModalNew_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm && sender is PasswordBox pb)
        {
            if (vm.NewPassword != pb.Password)
            {
                vm.NewPassword = pb.Password;
            }
        }
    }

    private void TxtModalNewPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm && sender is TextBox tb)
        {
            if (TxtModalNew.Password != tb.Text)
            {
                TxtModalNew.Password = tb.Text;
            }
        }
    }

    private void TxtModalConfirm_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm && sender is PasswordBox pb)
        {
            if (vm.ConfirmNewPassword != pb.Password)
            {
                vm.ConfirmNewPassword = pb.Password;
            }
        }
    }

    private void TxtModalConfirmPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm && sender is TextBox tb)
        {
            if (TxtModalConfirm.Password != tb.Text)
            {
                TxtModalConfirm.Password = tb.Text;
            }
        }
    }
}
