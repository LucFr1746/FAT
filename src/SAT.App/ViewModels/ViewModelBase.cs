using CommunityToolkit.Mvvm.ComponentModel;

namespace SAT.App.ViewModels;

/// <summary>
/// Lớp cha của mọi ViewModel. 🔒 Chủ sở hữu: TV1.
///
/// Kế thừa ObservableObject của CommunityToolkit để dùng [ObservableProperty]
/// và [RelayCommand] - trình sinh mã lo phần INotifyPropertyChanged, nên
/// ViewModel chỉ còn phần logic thật.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Tiêu đề hiển thị trên thanh trên cùng của shell.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Đang tải dữ liệu. Bind vào ProgressBar; mọi thao tác chạm DB đều phải
    /// bật cờ này, nếu không người dùng tưởng app bị treo.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Thông báo lỗi hiển thị ngay trong màn hình (không dùng MessageBox).</summary>
    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>
    /// Bọc một thao tác bất đồng bộ: tự bật/tắt IsBusy và bắt lỗi.
    ///
    /// Có hàm này để không ViewModel nào phải tự viết try/finally, và quan
    /// trọng hơn là để không ai quên tắt IsBusy trong nhánh lỗi - quên một
    /// lần là màn hình kẹt ở trạng thái loading vĩnh viễn.
    /// </summary>
    protected async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return; // Chặn double-click gửi hai lần cùng một thao tác.
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
