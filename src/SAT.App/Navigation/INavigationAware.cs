namespace SAT.App.Navigation;

/// <summary>
/// ViewModel cần nạp dữ liệu khi được điều hướng tới thì cài đặt interface này.
///
/// Nạp dữ liệu ở đây chứ KHÔNG nạp trong constructor: constructor không thể
/// await, ép phải dùng .Result hoặc async void - cả hai đều làm treo UI thread.
/// </summary>
public interface INavigationAware
{
    /// <param name="parameter">Tham số truyền từ lời gọi điều hướng, có thể null.</param>
    Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default);
}
