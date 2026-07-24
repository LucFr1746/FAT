using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using App.ViewModels.Common;
using Domain.Constants;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Catalog;

/// <summary>
/// Assign Subject to Major, and Curriculum Management.
///
/// One screen for both because they are the same task from two directions:
/// the left panel lists subjects not yet in the programme, the right panel the
/// curriculum itself, with reorder, bulk operations and export.
/// </summary>
public partial class CurriculumAdminViewModel : PagedListViewModel<CurriculumItemDto>, INavigationAware
{
    private readonly ICurriculumAdminService _curriculumAdmin;
    private readonly ICatalogAdminService _catalogAdmin;

    [ObservableProperty]
    private ObservableCollection<MajorDto> _majors = [];

    [ObservableProperty]
    private MajorDto? _selectedMajor;

    [ObservableProperty]
    private ObservableCollection<SubjectAdminViewModel.TermOption> _termOptions = [];

    [ObservableProperty]
    private SubjectAdminViewModel.TermOption? _selectedTermFilter;

    // ----- Assign panel -----
    [ObservableProperty]
    private ObservableCollection<SelectableCourse> _unassignedCourses = [];

    [ObservableProperty]
    private string? _assignSearchKeyword;

    [ObservableProperty]
    private int _assignTargetTermNo = 1;

    // ----- Curriculum selection -----
    [ObservableProperty]
    private ObservableCollection<CurriculumItemDto> _selectedItems = [];

    public bool HasMajorSelected => SelectedMajor is not null;

    partial void OnSelectedMajorChanged(MajorDto? value) => OnPropertyChanged(nameof(HasMajorSelected));

    /// <summary>Total credits of the programme - must equal Major.RequiredCredits.</summary>
    [ObservableProperty]
    private int _curriculumTotalCredits;

    public CurriculumAdminViewModel(
        ICurriculumAdminService curriculumAdmin, ICatalogAdminService catalogAdmin)
    {
        _curriculumAdmin = curriculumAdmin;
        _catalogAdmin = catalogAdmin;
        Title = "Quản Lý Chương Trình Học";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        TermOptions.Clear();
        TermOptions.Add(new SubjectAdminViewModel.TermOption(null, "Tất cả các kỳ"));
        for (var termNo = CatalogRules.MinTermNo; termNo <= 9; termNo++)
        {
            TermOptions.Add(new SubjectAdminViewModel.TermOption(termNo, CatalogRules.GetTermName(termNo)));
        }

        SelectedTermFilter = TermOptions.First();

        var majors = await _catalogAdmin.GetMajorsAsync(new MajorFilter(IsActive: true), cancellationToken);
        Majors.Clear();
        foreach (var major in majors)
        {
            Majors.Add(major);
        }

        SelectedMajor = Majors.FirstOrDefault();
        await ReloadAllAsync(cancellationToken);
    }

    protected override async Task<IReadOnlyList<CurriculumItemDto>> LoadItemsAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedMajor is null)
        {
            return [];
        }

        var items = await _curriculumAdmin.SearchAsync(
            new CurriculumFilter(
                MajorId: SelectedMajor.MajorId,
                TermNo: SelectedTermFilter?.TermNo,
                Keyword: SearchKeyword),
            cancellationToken);

        // Always the WHOLE programme, never just the filtered view: this figure
        // is compared against Major.RequiredCredits, and a filtered total would
        // look like a mismatch that is not there.
        var all = SelectedTermFilter?.TermNo is null && string.IsNullOrWhiteSpace(SearchKeyword)
            ? items
            : await _curriculumAdmin.GetByMajorAsync(SelectedMajor.MajorId, cancellationToken: cancellationToken);

        CurriculumTotalCredits = all.Sum(i => i.Credits);

        return items;
    }

    protected override IReadOnlyList<CurriculumItemDto> ApplySort(IReadOnlyList<CurriculumItemDto> items)
    {
        var sorted = SortColumn switch
        {
            nameof(CurriculumItemDto.CourseCode) => items.OrderBy(i => i.CourseCode).ToList(),
            nameof(CurriculumItemDto.CourseName) => items.OrderBy(i => i.CourseName).ToList(),
            nameof(CurriculumItemDto.Credits) => items.OrderBy(i => i.Credits).ToList(),
            _ => items.OrderBy(i => i.TermNo).ThenBy(i => i.DisplayOrder).ToList()
        };

        return SortDescending ? sorted.AsEnumerable().Reverse().ToList() : sorted;
    }

    private async Task ReloadAllAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken);
        await LoadUnassignedAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task ChangeMajorAsync() => await ReloadAllAsync();

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await RefreshAsync();

    // =========================================================================
    // Assign panel
    // =========================================================================

    private async Task LoadUnassignedAsync(CancellationToken cancellationToken = default)
    {
        UnassignedCourses.Clear();

        if (SelectedMajor is null)
        {
            return;
        }

        var courses = await _curriculumAdmin.GetUnassignedCoursesAsync(
            SelectedMajor.MajorId, AssignSearchKeyword, cancellationToken);

        foreach (var course in courses)
        {
            UnassignedCourses.Add(new SelectableCourse(course));
        }
    }

    [RelayCommand]
    private async Task SearchUnassignedAsync() => await LoadUnassignedAsync();

    [RelayCommand]
    private void SelectAllUnassigned()
    {
        foreach (var course in UnassignedCourses)
        {
            course.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearUnassignedSelection()
    {
        foreach (var course in UnassignedCourses)
        {
            course.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task AssignSelectedAsync()
    {
        if (SelectedMajor is null)
        {
            return;
        }

        var courseIds = UnassignedCourses.Where(c => c.IsSelected).Select(c => c.Course.CourseId).ToList();

        if (courseIds.Count == 0)
        {
            ErrorMessage = "Chưa chọn môn học nào để gán.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;

            var result = await _curriculumAdmin.BulkAssignAsync(
                SelectedMajor.MajorId, courseIds, AssignTargetTermNo);

            await ReloadAllAsync();

            // Skipped entries are surfaced, not swallowed: a bulk action that
            // silently drops half its input looks like it worked.
            StatusMessage = result.HasSkipped
                ? $"Đã gán {result.Succeeded} môn, bỏ qua {result.Skipped} môn. " +
                  string.Join(" ", result.Messages.Take(3))
                : $"Đã gán {result.Succeeded} môn vào {CatalogRules.GetTermName(AssignTargetTermNo)}.";
        });
    }

    // =========================================================================
    // Curriculum panel
    // =========================================================================

    [RelayCommand]
    private async Task RemoveItemAsync(CurriculumItemDto? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Xóa môn '{target.CourseCode} - {target.CourseName}' khỏi chương trình học?\n\n" +
            "Tổng số tín chỉ của ngành sẽ được tính lại.",
            "Xác nhận xóa khỏi chương trình",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            await _curriculumAdmin.RemoveAsync(target.CurriculumId);
            await ReloadAllAsync();
            StatusMessage = $"Đã xóa môn '{target.CourseCode}' khỏi chương trình học.";
        });
    }

    [RelayCommand]
    private async Task MoveUpAsync(CurriculumItemDto? item) => await MoveAsync(item, -1);

    [RelayCommand]
    private async Task MoveDownAsync(CurriculumItemDto? item) => await MoveAsync(item, +1);

    /// <summary>
    /// Moves a subject one place within its kỳ.
    ///
    /// The service demands the COMPLETE ordering of the kỳ, so the whole kỳ is
    /// fetched and reordered here - sending only the two swapped ids would leave
    /// every other subject on a stale position.
    /// </summary>
    private async Task MoveAsync(CurriculumItemDto? item, int offset)
    {
        var target = item ?? SelectedItem;
        if (target is null || SelectedMajor is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;

            var termItems = (await _curriculumAdmin.GetByMajorAsync(SelectedMajor.MajorId, target.TermNo))
                .OrderBy(i => i.DisplayOrder)
                .ToList();

            var index = termItems.FindIndex(i => i.CurriculumId == target.CurriculumId);
            var newIndex = index + offset;

            if (index < 0 || newIndex < 0 || newIndex >= termItems.Count)
            {
                return;
            }

            (termItems[index], termItems[newIndex]) = (termItems[newIndex], termItems[index]);

            await _curriculumAdmin.ReorderAsync(
                SelectedMajor.MajorId, target.TermNo, termItems.Select(i => i.CurriculumId).ToList());

            await RefreshAsync();
            StatusMessage = $"Đã đổi vị trí môn '{target.CourseCode}'.";
        });
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedMajor is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Xuất chương trình học",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"curriculum_{SelectedMajor.MajorCode}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;
            var csv = await _curriculumAdmin.ExportToCsvAsync(SelectedMajor.MajorId);

            // UTF-8 WITHOUT a second BOM: ExportToCsvAsync already writes one, so
            // letting the encoder add another would show a stray character in
            // the first cell.
            await File.WriteAllTextAsync(dialog.FileName, csv, new UTF8Encoding(false));

            StatusMessage = $"Đã xuất chương trình học ra '{dialog.FileName}'.";
        });
    }

    /// <summary>A subject in the assign list, with its checkbox state.</summary>
    public partial class SelectableCourse : ObservableObject
    {
        public SelectableCourse(CourseDto course) => Course = course;

        public CourseDto Course { get; }

        [ObservableProperty]
        private bool _isSelected;
    }
}
