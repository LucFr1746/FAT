using SAT.App.ViewModels;

namespace SAT.App.Navigation;

/// <summary>
/// Điều hướng giữa các màn hình. 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: TV1.
///
/// VÌ SAO KHÔNG DÙNG Frame / NavigationWindow:
/// Frame điều hướng theo View trước (trỏ tới file XAML qua URI), nên rất khó
/// lấy ViewModel ra từ DI container và khó truyền tham số. Cách ở đây là
/// ViewModel trước: NavigateToAsync&lt;TViewModel&gt;() lấy ViewModel từ container,
/// View được ghép vào bằng DataTemplate. Nhờ vậy điều hướng an toàn kiểu, và
/// ViewModel nhận đủ dependency đã inject sẵn (docs/plan §4).
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;

    /// <summary>Dùng khi kiểu ViewModel chỉ biết lúc chạy, ví dụ khi bấm menu.</summary>
    Task NavigateToAsync(Type viewModelType, object? parameter = null);

    bool CanGoBack { get; }
    Task GoBackAsync();

    /// <summary>Xóa lịch sử điều hướng. Gọi khi đăng xuất.</summary>
    void ClearHistory();
}
