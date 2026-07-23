using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.App.ViewModels.Common;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Catalog;

/// <summary>
/// Manage Major. Search, filter, sort, page, create, edit and deactivate.
///
/// Follows the shape of UserManagementViewModel - inline banners rather than
/// message boxes for outcomes, a MessageBox only to confirm something
/// destructive.
/// </summary>
public partial class MajorAdminViewModel : PagedListViewModel<MajorDto>, INavigationAware
{
    private readonly ICatalogAdminService _catalogAdmin;

    [ObservableProperty]
    private bool _showInactive;

    // ----- Editor state -----
    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _editMajorCode = string.Empty;

    [ObservableProperty]
    private string _editMajorName = string.Empty;

    [ObservableProperty]
    private int _editTotalTerms = 9;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private string? _editorErrorMessage;

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorErrorMessage);

    partial void OnEditorErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasEditorError));

    private int _editingMajorId;

    public MajorAdminViewModel(ICatalogAdminService catalogAdmin)
    {
        _catalogAdmin = catalogAdmin;
        Title = "Quản Lý Ngành Học";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => await RefreshAsync(cancellationToken);

    protected override async Task<IReadOnlyList<MajorDto>> LoadItemsAsync(CancellationToken cancellationToken)
        => await _catalogAdmin.GetMajorsAsync(
            new MajorFilter(SearchKeyword, ShowInactive ? null : true), cancellationToken);

    protected override IReadOnlyList<MajorDto> ApplySort(IReadOnlyList<MajorDto> items)
    {
        var sorted = SortColumn switch
        {
            nameof(MajorDto.MajorName) => items.OrderBy(m => m.MajorName).ToList(),
            nameof(MajorDto.RequiredCredits) => items.OrderBy(m => m.RequiredCredits).ToList(),
            nameof(MajorDto.TotalTerms) => items.OrderBy(m => m.TotalTerms).ToList(),
            _ => items.OrderBy(m => m.MajorCode).ToList()
        };

        return SortDescending ? sorted.AsEnumerable().Reverse().ToList() : sorted;
    }

    [RelayCommand]
    private async Task ToggleShowInactiveAsync()
    {
        ShowInactive = !ShowInactive;
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenCreateEditor()
    {
        IsCreating = true;
        _editingMajorId = 0;
        EditMajorCode = string.Empty;
        EditMajorName = string.Empty;
        EditTotalTerms = 9;
        EditIsActive = true;
        EditorErrorMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditEditor(MajorDto? major)
    {
        var target = major ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        IsCreating = false;
        _editingMajorId = target.MajorId;
        EditMajorCode = target.MajorCode;
        EditMajorName = target.MajorName;
        EditTotalTerms = target.TotalTerms;
        EditIsActive = target.IsActive;
        EditorErrorMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        EditorErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        EditorErrorMessage = null;

        // Checked here as well as in the service so the user gets the message
        // without a round trip; the service check is the one that actually
        // protects the data.
        if (string.IsNullOrWhiteSpace(EditMajorCode))
        {
            EditorErrorMessage = "* Mã ngành không được để trống.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditMajorName))
        {
            EditorErrorMessage = "* Tên ngành không được để trống.";
            return;
        }

        var dto = new MajorDto(
            _editingMajorId,
            EditMajorCode.Trim(),
            EditMajorName.Trim(),
            // RequiredCredits is derived from the curriculum, never typed in -
            // the service recomputes it and ignores whatever is passed here.
            RequiredCredits: 1,
            TotalTerms: EditTotalTerms,
            IsActive: EditIsActive);

        try
        {
            if (IsCreating)
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.CreateMajorAsync(dto),
                    $"Đã thêm ngành '{dto.MajorCode}' thành công.");
            }
            else
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.UpdateMajorAsync(dto),
                    $"Đã cập nhật ngành '{dto.MajorCode}' thành công.");
            }

            // Only closed on success: leaving the form open on failure keeps the
            // user's input so they can correct it.
            if (!HasError)
            {
                IsEditorOpen = false;
            }
            else
            {
                EditorErrorMessage = ErrorMessage;
                ErrorMessage = null;
            }
        }
        catch (Exception ex)
        {
            EditorErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(MajorDto? major)
    {
        var target = major ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Bạn có chắc chắn muốn ngừng hoạt động ngành '{target.MajorCode} - {target.MajorName}' không?\n\n" +
            "Ngành sẽ bị ẩn khỏi danh sách đăng ký nhưng dữ liệu chương trình học được giữ nguyên.",
            "Xác nhận ngừng hoạt động",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunAndRefreshAsync(
            () => _catalogAdmin.DeactivateMajorAsync(target.MajorId),
            $"Đã ngừng hoạt động ngành '{target.MajorCode}'.");
    }
}
