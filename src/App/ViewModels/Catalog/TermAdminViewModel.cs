using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using App.ViewModels.Common;
using Domain.Constants;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Catalog;

/// <summary>
/// Manage Semester - CRUD over the kỳ of the study path (Kỳ 0 .. Kỳ 9).
///
/// The screen says so explicitly, because a "Semester" menu next to the calendar
/// semesters (FA25, SP26) is otherwise easy to mistake for the same thing.
/// </summary>
public partial class TermAdminViewModel : PagedListViewModel<TermDto>, INavigationAware
{
    private readonly ITermService _termService;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private int _editTermNo;

    [ObservableProperty]
    private string _editTermName = string.Empty;

    [ObservableProperty]
    private string? _editDescription;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private string? _editorErrorMessage;

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorErrorMessage);

    partial void OnEditorErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasEditorError));

    private int _editingTermId;

    public TermAdminViewModel(ITermService termService)
    {
        _termService = termService;
        Title = "Quản Lý Kỳ Học (Chương Trình Đào Tạo)";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => await RefreshAsync(cancellationToken);

    protected override async Task<IReadOnlyList<TermDto>> LoadItemsAsync(CancellationToken cancellationToken)
    {
        var terms = await _termService.GetAllAsync(includeInactive: true, cancellationToken);

        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            return terms;
        }

        var keyword = SearchKeyword.Trim();

        return terms
            .Where(t => t.TermName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                     || t.TermNo.ToString().Contains(keyword, StringComparison.Ordinal)
                     || (t.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    protected override IReadOnlyList<TermDto> ApplySort(IReadOnlyList<TermDto> items)
    {
        var sorted = SortColumn switch
        {
            nameof(TermDto.TermName) => items.OrderBy(t => t.TermName).ToList(),
            nameof(TermDto.SubjectCount) => items.OrderBy(t => t.SubjectCount).ToList(),
            _ => items.OrderBy(t => t.TermNo).ToList()
        };

        return SortDescending ? sorted.AsEnumerable().Reverse().ToList() : sorted;
    }

    [RelayCommand]
    private async Task OpenCreateEditorAsync()
    {
        IsCreating = true;
        _editingTermId = 0;

        // Suggests the next free number so the common case needs no typing.
        var existing = await _termService.GetAllAsync();
        var nextTermNo = existing.Count == 0 ? 0 : existing.Max(t => t.TermNo) + 1;

        EditTermNo = Math.Min(nextTermNo, CatalogRules.MaxTermNo);
        EditTermName = CatalogRules.GetTermName(EditTermNo);
        EditDescription = null;
        EditIsActive = true;
        EditorErrorMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditEditor(TermDto? term)
    {
        var target = term ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        IsCreating = false;
        _editingTermId = target.TermId;
        EditTermNo = target.TermNo;
        EditTermName = target.TermName;
        EditDescription = target.Description;
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

        if (EditTermNo < CatalogRules.MinTermNo || EditTermNo > CatalogRules.MaxTermNo)
        {
            EditorErrorMessage =
                $"* Số kỳ phải nằm trong khoảng {CatalogRules.MinTermNo} đến {CatalogRules.MaxTermNo}.";
            return;
        }

        var dto = new TermDto(
            _editingTermId,
            EditTermNo,
            string.IsNullOrWhiteSpace(EditTermName) ? CatalogRules.GetTermName(EditTermNo) : EditTermName.Trim(),
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
            EditIsActive);

        try
        {
            if (IsCreating)
            {
                await RunAndRefreshAsync(
                    () => _termService.CreateAsync(dto), $"Đã thêm {dto.TermName} thành công.");
            }
            else
            {
                await RunAndRefreshAsync(
                    () => _termService.UpdateAsync(dto), $"Đã cập nhật {dto.TermName} thành công.");
            }

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
    private async Task ToggleActiveAsync(TermDto? term)
    {
        var target = term ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        await RunAndRefreshAsync(
            () => _termService.SetActiveAsync(target.TermId, !target.IsActive),
            $"Đã {(target.IsActive ? "ngừng hoạt động" : "kích hoạt")} {target.TermName}.");
    }

    [RelayCommand]
    private async Task DeleteAsync(TermDto? term)
    {
        var target = term ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        // Warned about up front rather than letting the service refuse it: the
        // user should know the constraint before committing to the action.
        if (target.SubjectCount > 0)
        {
            ErrorMessage =
                $"Không thể xóa {target.TermName}: đang có {target.SubjectCount} môn học thuộc kỳ này. " +
                "Hãy dùng chức năng Ngừng hoạt động nếu chỉ muốn ẩn kỳ học.";
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa {target.TermName} không? Thao tác này không thể hoàn tác.",
            "Xác nhận xóa kỳ học",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunAndRefreshAsync(
            () => _termService.DeleteAsync(target.TermId), $"Đã xóa {target.TermName}.");
    }
}
