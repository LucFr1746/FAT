using CommunityToolkit.Mvvm.ComponentModel;

namespace App.ViewModels;

/// <summary>
/// Base class for every view model. Owner: Member 1.
///
/// Derives from CommunityToolkit's ObservableObject so that [ObservableProperty]
/// and [RelayCommand] are available - the source generator writes the
/// INotifyPropertyChanged plumbing, leaving only real logic in the view model.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Title shown in the shell's top bar.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Whether data is loading. Bind it to a ProgressBar; every operation that
    /// touches the database must set it, otherwise the app looks frozen.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Error text shown inline in the screen (never a MessageBox).</summary>
    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>
    /// Wraps an async operation: toggles IsBusy and captures errors.
    ///
    /// This exists so no view model has to hand-roll try/finally, and more
    /// importantly so nobody forgets to clear IsBusy on the failure path -
    /// forget it once and the screen is stuck on a spinner forever.
    /// </summary>
    protected async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return; // Swallow double-clicks that would run the same work twice.
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
