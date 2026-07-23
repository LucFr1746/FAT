using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.App.ViewModels.Common;
using FAT.Domain.Constants;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.App.ViewModels.Catalog;

/// <summary>
/// Manage Subject: the subject list and its editor.
///
/// The three detail panels - materials, grade structure and assessment schedule
/// - live in SubjectAdminViewModel.Details.cs to keep each file readable.
/// </summary>
public partial class SubjectAdminViewModel : PagedListViewModel<CourseDto>, INavigationAware
{
    private readonly ICatalogAdminService _catalogAdmin;
    private readonly ICurriculumAdminService _curriculumAdmin;
    private readonly ISubjectMaterialService _materialService;
    private readonly IGradeStructureService _gradeStructureService;
    private readonly IAssessmentScheduleService _scheduleService;

    // ----- Filters -----
    [ObservableProperty]
    private bool _showInactive;

    [ObservableProperty]
    private ObservableCollection<MajorDto> _majors = [];

    [ObservableProperty]
    private MajorDto? _selectedMajorFilter;

    [ObservableProperty]
    private ObservableCollection<TermOption> _termOptions = [];

    [ObservableProperty]
    private TermOption? _selectedTermFilter;

    // ----- Subject editor -----
    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _editCourseCode = string.Empty;

    [ObservableProperty]
    private string _editCourseName = string.Empty;

    [ObservableProperty]
    private int _editCredits = 3;

    [ObservableProperty]
    private string? _editDescription;

    [ObservableProperty]
    private bool _editCountsTowardGpa = true;

    [ObservableProperty]
    private string? _editPrerequisiteText;

    [ObservableProperty]
    private bool _editIsActive = true;

    [ObservableProperty]
    private string? _editorErrorMessage;

    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorErrorMessage);

    partial void OnEditorErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasEditorError));

    private int _editingCourseId;

    /// <summary>Which detail panel is showing: Materials | GradeStructure | Schedule.</summary>
    [ObservableProperty]
    private string _activeDetailTab = "Materials";

    [ObservableProperty]
    private CourseDto? _detailCourse;

    public bool IsDetailOpen => DetailCourse is not null;

    partial void OnDetailCourseChanged(CourseDto? value) => OnPropertyChanged(nameof(IsDetailOpen));

    public bool IsMaterialsTab => ActiveDetailTab == "Materials";
    public bool IsGradeStructureTab => ActiveDetailTab == "GradeStructure";
    public bool IsScheduleTab => ActiveDetailTab == "Schedule";

    partial void OnActiveDetailTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsMaterialsTab));
        OnPropertyChanged(nameof(IsGradeStructureTab));
        OnPropertyChanged(nameof(IsScheduleTab));
    }

    public SubjectAdminViewModel(
        ICatalogAdminService catalogAdmin,
        ICurriculumAdminService curriculumAdmin,
        ISubjectMaterialService materialService,
        IGradeStructureService gradeStructureService,
        IAssessmentScheduleService scheduleService)
    {
        _catalogAdmin = catalogAdmin;
        _curriculumAdmin = curriculumAdmin;
        _materialService = materialService;
        _gradeStructureService = gradeStructureService;
        _scheduleService = scheduleService;
        Title = "Quản Lý Môn Học";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        await LoadFilterOptionsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    protected override async Task<IReadOnlyList<CourseDto>> LoadItemsAsync(CancellationToken cancellationToken)
        => await _catalogAdmin.GetCoursesAsync(
            new CourseFilter(
                Keyword: SearchKeyword,
                MajorId: SelectedMajorFilter?.MajorId,
                // TermNo 0 is a real kỳ, so "no filter" has to be null rather
                // than zero - a sentinel of 0 would silently hide Kỳ 0.
                TermNo: SelectedTermFilter?.TermNo,
                IsActive: ShowInactive ? null : true),
            cancellationToken);

    protected override IReadOnlyList<CourseDto> ApplySort(IReadOnlyList<CourseDto> items)
    {
        var sorted = SortColumn switch
        {
            nameof(CourseDto.CourseName) => items.OrderBy(c => c.CourseName).ToList(),
            nameof(CourseDto.Credits) => items.OrderBy(c => c.Credits).ToList(),
            nameof(CourseDto.CountsTowardGpa) => items.OrderBy(c => c.CountsTowardGpa).ToList(),
            _ => items.OrderBy(c => c.CourseCode).ToList()
        };

        return SortDescending ? sorted.AsEnumerable().Reverse().ToList() : sorted;
    }

    private async Task LoadFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var majors = await _catalogAdmin.GetMajorsAsync(new MajorFilter(IsActive: true), cancellationToken);

        Majors.Clear();
        foreach (var major in majors)
        {
            Majors.Add(major);
        }

        TermOptions.Clear();
        // A null TermNo means "every kỳ"; see the note in LoadItemsAsync.
        TermOptions.Add(new TermOption(null, "Tất cả các kỳ"));
        for (var termNo = CatalogRules.MinTermNo; termNo <= 9; termNo++)
        {
            TermOptions.Add(new TermOption(termNo, CatalogRules.GetTermName(termNo)));
        }

        SelectedTermFilter = TermOptions.First();
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        SearchKeyword = null;
        SelectedMajorFilter = null;
        SelectedTermFilter = TermOptions.FirstOrDefault();
        ShowInactive = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ToggleShowInactiveAsync()
    {
        ShowInactive = !ShowInactive;
        await RefreshAsync();
    }

    // =========================================================================
    // Subject editor
    // =========================================================================

    [RelayCommand]
    private void OpenCreateEditor()
    {
        IsCreating = true;
        _editingCourseId = 0;
        EditCourseCode = string.Empty;
        EditCourseName = string.Empty;
        EditCredits = 3;
        EditDescription = null;
        EditCountsTowardGpa = true;
        EditPrerequisiteText = null;
        EditIsActive = true;
        EditorErrorMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditEditor(CourseDto? course)
    {
        var target = course ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        IsCreating = false;
        _editingCourseId = target.CourseId;
        EditCourseCode = target.CourseCode;
        EditCourseName = target.CourseName;
        EditCredits = target.Credits;
        EditDescription = target.Description;
        EditCountsTowardGpa = target.CountsTowardGpa;
        EditPrerequisiteText = target.PrerequisiteText;
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

        if (string.IsNullOrWhiteSpace(EditCourseCode))
        {
            EditorErrorMessage = "* Mã môn học không được để trống.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCourseName))
        {
            EditorErrorMessage = "* Tên môn học không được để trống.";
            return;
        }

        if (EditCredits < CatalogRules.MinCredits || EditCredits > CatalogRules.MaxCredits)
        {
            EditorErrorMessage =
                $"* Số tín chỉ phải nằm trong khoảng {CatalogRules.MinCredits} đến {CatalogRules.MaxCredits}.";
            return;
        }

        var dto = new CourseDto(
            _editingCourseId,
            EditCourseCode.Trim().ToUpperInvariant(),
            EditCourseName.Trim(),
            EditCredits,
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim(),
            EditIsActive,
            PrerequisiteCount: 0,
            CountsTowardGpa: EditCountsTowardGpa,
            MinAvgMarkToPass: null,
            PrerequisiteText: string.IsNullOrWhiteSpace(EditPrerequisiteText) ? null : EditPrerequisiteText.Trim());

        try
        {
            if (IsCreating)
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.CreateCourseAsync(dto),
                    $"Đã thêm môn học '{dto.CourseCode}' thành công.");
            }
            else
            {
                await RunAndRefreshAsync(
                    () => _catalogAdmin.UpdateCourseAsync(dto),
                    $"Đã cập nhật môn học '{dto.CourseCode}' thành công.");
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
    private async Task DeactivateAsync(CourseDto? course)
    {
        var target = course ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Bạn có chắc chắn muốn ngừng hoạt động môn '{target.CourseCode} - {target.CourseName}' không?\n\n" +
            "Môn học sẽ bị ẩn khỏi chương trình học nhưng bảng điểm của sinh viên được giữ nguyên.",
            "Xác nhận ngừng hoạt động",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunAndRefreshAsync(
            () => _catalogAdmin.DeactivateCourseAsync(target.CourseId),
            $"Đã ngừng hoạt động môn '{target.CourseCode}'.");
    }

    /// <summary>One entry of the kỳ filter. A null number means "every kỳ".</summary>
    public sealed record TermOption(int? TermNo, string Display);
}
