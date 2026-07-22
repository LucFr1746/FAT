using MaterialDesignThemes.Wpf;

namespace SAT.App.Navigation;

/// <summary>
/// Một mục trên thanh điều hướng bên trái.
///
/// 👉 ĐIỂM CHỐNG CONFLICT QUAN TRỌNG (docs/plan §4):
/// Sidebar được SINH RA từ danh sách các NavigationItem mà mỗi module tự đăng
/// ký trong file Startup/&lt;Module&gt;Registration.cs của mình. Menu KHÔNG được
/// viết cứng trong MainWindow.xaml.
///
/// Nhờ vậy 5 người cùng thêm màn hình mới mà không ai phải sửa chung một file
/// XAML - đây chính là chỗ hay conflict nhất trong đồ án nhóm WPF.
/// </summary>
/// <param name="Title">Chữ hiển thị trên menu.</param>
/// <param name="Icon">Icon Material Design.</param>
/// <param name="ViewModelType">ViewModel sẽ được điều hướng tới.</param>
/// <param name="Order">Thứ tự trong menu; số nhỏ nằm trên.</param>
/// <param name="RequiresAdmin">True thì chỉ tài khoản Admin nhìn thấy mục này.</param>
/// <param name="Group">Tên nhóm để phân tách menu, ví dụ "Quản trị".</param>
public sealed record NavigationItem(
    string Title,
    PackIconKind Icon,
    Type ViewModelType,
    int Order,
    bool RequiresAdmin = false,
    string? Group = null);
