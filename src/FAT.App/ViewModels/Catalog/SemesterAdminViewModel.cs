using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.App.ViewModels.Common;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Catalog;

/// <summary>
/// CRUD over the CALENDAR semesters (FA25, SP26) that enrollments are recorded
/// against.
///
/// Distinct from <see cref="TermAdminViewModel"/>, which manages the kỳ of the
/// study path. Both screens say which is which in their subtitle.
/// </summary>
public partial class SemesterAdminViewModel : PagedListViewModel<SemesterDto>, INavigationAware
{
    private readonly ICatalogAdminService _catalogAdmin;
    private readonly ICourseService _courseService;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _editSemesterCode = string.Empty;

    [ObservableProperty]
    private string _editSemesterName = string.Empty;

    [ObservableProperty]
    private DateTime _editStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _editEndDate = DateTime.Today.AddMonths(4);

    [ObservableProperty]
    private int _editDisplayOrder = 1;

    [ObservableProperty]
    private string? _editorErrorMessage;

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorErrorMessage);

    partial void OnEditorErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasEditorError));

    private int _editingSemesterId;

    public SemesterAdminViewModel(ICatalogAdminService catalogAdmin, ICourseService courseService)
    {
        _catalogAdmin = catalogAdmin;
        _courseService = courseService;
        Title = "Quản Lý Học Kỳ (Theo Lịch)";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => await RefreshAsync(cancellationToken);

    protected override async Task<IReadOnlyList<SemesterDto>> LoadItemsAsync(CancellationToken cancellationToken)
    {
        var semesters = await _courseService.GetSemestersAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            return semesters;
        }

        var keyword = SearchKeyword.Trim();

        return semesters
            .Where(s => s.SemesterCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                     || s.SemesterName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    protected override IReadOnlyList<SemesterDto> ApplySort(IReadOnlyList<SemesterDto> items)
    {
        var sorted = SortColumn switch
        {
            nameof(SemesterDto.SemesterCode) => items.OrderBy(s => s.SemesterCode).ToList(),
            nameof(SemesterDto.StartDate) => items.OrderBy(s => s.StartDate).ToList(),
            // DisplayOrder is the true chronological order; sorting by code would
            // put FA25 before SP26 even though FA25 happens first.
            _ => items.OrderBy(s => s.DisplayOrder).ToList()
        };

        return SortDescending ? sorted.AsEnumerable().Reverse().ToList() : sorted;
    }

    [RelayCommand]
    private async Task OpenCreateEditorAsync()
    {
        IsCreating = true;
        _editingSemesterId = 0;

        var existing = await _courseService.GetSemestersAsync();
        var last = existing.OrderByDescending(s => s.DisplayOrder).FirstOrDefault();

        EditSemesterCode = string.Empty;
        EditSemesterName = string.Empty;
        // Continues from the last semester so the dates line up without typing.
        EditStartDate = last?.EndDate.AddDays(7) ?? DateTime.Today;
        EditEndDate = EditStartDate.AddMonths(4);
        EditDisplayOrder = (last?.DisplayOrder ?? 0) + 1;
        EditorErrorMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditEditor(SemesterDto? semester)
    {
        var target = semester ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        IsCreating = false;
        _editingSemesterId = target.SemesterId;
        EditSemesterCode = target.SemesterCode;
        EditSemesterName = target.SemesterName;
        EditStartDate = target.StartDate;
        EditEndDate = target.EndDate;
        EditDisplayOrder = target.DisplayOrder;
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

        if (string.IsNullOrWhiteSpace(EditSemesterCode))
        {
            EditorErrorMessage = "* Mã học kỳ không được để trống.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditSemesterName))
        {
            EditorErrorMessage = "* Tên học kỳ không được để trống.";
            return;
        }

        if (EditEndDate <= EditStartDate)
        {
            EditorErrorMessage = "* Ngày kết thúc phải sau ngày bắt đầu.";
            return;
        }

        var dto = new SemesterDto(
            _editingSemesterId,
            EditSemesterCode.Trim(),
            EditSemesterName.Trim(),
            EditStartDate,
            EditEndDate,
            EditDisplayOrder,
            // Never set through the editor: exactly one semester may be current,
            // and SetCurrentSemesterAsync is the only place that rule lives.
            IsCurrent: false);

        try
        {
            if (IsCreating)
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.CreateSemesterAsync(dto),
                    $"Đã thêm học kỳ '{dto.SemesterCode}' thành công.");
            }
            else
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.UpdateSemesterAsync(dto),
                    $"Đã cập nhật học kỳ '{dto.SemesterCode}' thành công.");
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
    private async Task SetCurrentAsync(SemesterDto? semester)
    {
        var target = semester ?? SelectedItem;
        if (target is null || target.IsCurrent)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Đặt '{target.SemesterCode} - {target.SemesterName}' làm học kỳ hiện tại?\n\n" +
            "Học kỳ hiện tại cũ sẽ được bỏ đánh dấu.",
            "Xác nhận đổi học kỳ hiện tại",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunAndRefreshAsync(
            () => _catalogAdmin.SetCurrentSemesterAsync(target.SemesterId),
            $"Học kỳ hiện tại là '{target.SemesterCode}'.");
    }
}
