using System.Windows;

namespace App;

/// <summary>
/// Application shell.
///
/// Code-behind stays empty on purpose: all logic belongs in the view model and
/// reaches the view through data binding.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
