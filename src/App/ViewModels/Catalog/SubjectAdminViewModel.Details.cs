using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Constants;
using Services.Dtos;

namespace App.ViewModels.Catalog;

/// <summary>
/// The three detail panels of Manage Subject: readings, grade structure and
/// assessment schedule.
///
/// One subject is open at a time (<see cref="DetailCourse"/>), and the panels
/// share that context rather than each holding their own copy - two panels
/// disagreeing about which subject is open is a bug the user cannot diagnose.
/// </summary>
public partial class SubjectAdminViewModel
{
    // ----- Materials -----
    [ObservableProperty]
    private ObservableCollection<SubjectMaterialDto> _materials = [];

    [ObservableProperty]
    private SubjectMaterialDto? _selectedMaterial;

    [ObservableProperty]
    private bool _isMaterialEditorOpen;

    [ObservableProperty]
    private string _materialTitle = string.Empty;

    [ObservableProperty]
    private string? _materialDescription;

    [ObservableProperty]
    private string? _materialUrl;

    private int _editingMaterialId;

    // The editor shows title, description and URL only, but UpdateAsync writes
    // every field of the DTO onto the entity. Carried through so that editing a
    // title does not erase the bibliographic data the FLM import brought in.
    private string? _editingMaterialAuthor;
    private string? _editingMaterialPublisher;
    private string? _editingMaterialIsbn;
    private int _editingMaterialDisplayOrder;
    private bool _editingMaterialIsActive = true;

    // ----- Grade structure -----
    [ObservableProperty]
    private ObservableCollection<AssessmentDto> _gradeColumns = [];

    [ObservableProperty]
    private AssessmentDto? _selectedGradeColumn;

    [ObservableProperty]
    private bool _isGradeColumnEditorOpen;

    [ObservableProperty]
    private string _gradeColumnName = string.Empty;

    [ObservableProperty]
    private decimal _gradeColumnWeightPercent = 10m;

    [ObservableProperty]
    private decimal? _gradeColumnMinScore;

    [ObservableProperty]
    private GradeStructureValidationDto? _gradeStructureValidation;

    private int _editingGradeColumnId;

    // "PT 3 bài" - how many pieces of work make up the component. Not in the
    // editor, but UpdateAsync writes it, so without carrying it through every
    // edit silently collapses the component back to a single part.
    private int _editingGradeColumnPartCount = 1;

    /// <summary>
    /// True when the weights do not add up. Bound to a warning banner, because
    /// nothing at run time notices an unbalanced structure - it just quietly
    /// caps the subject's final score.
    /// </summary>
    public bool HasGradeStructureWarning =>
        GradeStructureValidation is not null && !GradeStructureValidation.IsBalanced && GradeColumns.Count > 0;

    partial void OnGradeStructureValidationChanged(GradeStructureValidationDto? value)
        => OnPropertyChanged(nameof(HasGradeStructureWarning));

    // ----- Schedule -----
    [ObservableProperty]
    private ObservableCollection<AssessmentScheduleDto> _scheduleItems = [];

    [ObservableProperty]
    private AssessmentScheduleDto? _selectedScheduleItem;

    [ObservableProperty]
    private bool _isScheduleEditorOpen;

    [ObservableProperty]
    private int _scheduleSessionNo = 1;

    [ObservableProperty]
    private string _scheduleTitle = string.Empty;

    [ObservableProperty]
    private string? _scheduleDescription;

    [ObservableProperty]
    private DateTime? _scheduleExpectedDate;

    [ObservableProperty]
    private string? _scheduleTeachingType;

    private int _editingScheduleId;

    // ----- Shared detail-panel error -----
    [ObservableProperty]
    private string? _detailErrorMessage;

    public bool HasDetailError => !string.IsNullOrWhiteSpace(DetailErrorMessage);

    partial void OnDetailErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasDetailError));

    // =========================================================================
    // Opening and closing the detail area
    // =========================================================================

    [RelayCommand]
    private async Task OpenDetailsAsync(CourseDto? course)
    {
        var target = course ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        DetailCourse = target;
        ActiveDetailTab = "Materials";
        DetailErrorMessage = null;

        await ReloadDetailsAsync();
    }

    [RelayCommand]
    private void CloseDetails()
    {
        DetailCourse = null;
        DetailErrorMessage = null;
        Materials.Clear();
        GradeColumns.Clear();
        ScheduleItems.Clear();
    }

    [RelayCommand]
    private void SwitchDetailTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            ActiveDetailTab = tab;
            DetailErrorMessage = null;
        }
    }

    /// <summary>Reloads all three panels for the open subject.</summary>
    private async Task ReloadDetailsAsync()
    {
        if (DetailCourse is null)
        {
            return;
        }

        var courseId = DetailCourse.CourseId;

        await RunBusyAsync(async () =>
        {
            Replace(Materials, await _materialService.GetByCourseAsync(courseId, includeInactive: true));
            Replace(GradeColumns, await _gradeStructureService.GetByCourseAsync(courseId));
            Replace(ScheduleItems, await _scheduleService.GetByCourseAsync(courseId));

            GradeStructureValidation = await _gradeStructureService.ValidateWeightsAsync(courseId);
            OnPropertyChanged(nameof(HasGradeStructureWarning));

            // DetailCourse was captured from the grid row that opened the panel,
            // so its counts date from before anything in the panel was changed.
            // Re-read it rather than let the header contradict the list below it.
            DetailCourse = await _catalogAdmin.GetCourseAsync(courseId) ?? DetailCourse;
        });
    }

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    /// <summary>
    /// Runs a detail-panel action and reloads. Errors land in
    /// <see cref="DetailErrorMessage"/> so they appear next to the panel that
    /// raised them rather than at the top of the page.
    /// </summary>
    private async Task RunDetailActionAsync(Func<Task> action, string successMessage)
    {
        DetailErrorMessage = null;

        try
        {
            await action();
            await ReloadDetailsAsync();

            // The list behind the panel carries "Số tài liệu" and "Số cột điểm"
            // columns fed by the same rows that were just changed. Without this
            // the grid keeps showing the counts from before the edit, which
            // reads as the save having failed.
            await ReloadKeepingPageAsync();

            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            DetailErrorMessage = ex.Message;
        }
    }

    // =========================================================================
    // Materials
    // =========================================================================

    [RelayCommand]
    private void OpenCreateMaterial()
    {
        _editingMaterialId = 0;
        MaterialTitle = string.Empty;
        MaterialDescription = null;
        MaterialUrl = null;
        _editingMaterialAuthor = null;
        _editingMaterialPublisher = null;
        _editingMaterialIsbn = null;
        _editingMaterialDisplayOrder = Materials.Count;
        _editingMaterialIsActive = true;
        DetailErrorMessage = null;
        IsMaterialEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditMaterial(SubjectMaterialDto? material)
    {
        var target = material ?? SelectedMaterial;
        if (target is null)
        {
            return;
        }

        _editingMaterialId = target.SubjectMaterialId;
        MaterialTitle = target.Title;
        MaterialDescription = target.Description;
        MaterialUrl = target.Url;
        _editingMaterialAuthor = target.Author;
        _editingMaterialPublisher = target.Publisher;
        _editingMaterialIsbn = target.Isbn;
        _editingMaterialDisplayOrder = target.DisplayOrder;
        _editingMaterialIsActive = target.IsActive;
        DetailErrorMessage = null;
        IsMaterialEditorOpen = true;
    }

    [RelayCommand]
    private void CloseMaterialEditor() => IsMaterialEditorOpen = false;

    [RelayCommand]
    private async Task SaveMaterialAsync()
    {
        if (DetailCourse is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(MaterialTitle))
        {
            DetailErrorMessage = "* Tiêu đề tài liệu không được để trống.";
            return;
        }

        var dto = new SubjectMaterialDto(
            _editingMaterialId,
            DetailCourse.CourseId,
            MaterialTitle.Trim(),
            string.IsNullOrWhiteSpace(MaterialDescription) ? null : MaterialDescription.Trim(),
            string.IsNullOrWhiteSpace(MaterialUrl) ? null : MaterialUrl.Trim(),
            // Carried through untouched - see the note on the backing fields.
            Author: _editingMaterialAuthor,
            Publisher: _editingMaterialPublisher,
            Isbn: _editingMaterialIsbn,
            DisplayOrder: _editingMaterialDisplayOrder,
            IsActive: _editingMaterialIsActive);

        await RunDetailActionAsync(
            () => _editingMaterialId == 0
                ? _materialService.CreateAsync(dto)
                : _materialService.UpdateAsync(dto),
            _editingMaterialId == 0 ? "Đã thêm tài liệu." : "Đã cập nhật tài liệu.");

        if (!HasDetailError)
        {
            IsMaterialEditorOpen = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMaterialAsync(SubjectMaterialDto? material)
    {
        var target = material ?? SelectedMaterial;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Xóa tài liệu '{target.Title}'?",
            "Xác nhận xóa tài liệu",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunDetailActionAsync(
            () => _materialService.DeleteAsync(target.SubjectMaterialId), "Đã xóa tài liệu.");
    }

    // =========================================================================
    // Grade structure
    // =========================================================================

    [RelayCommand]
    private void OpenCreateGradeColumn()
    {
        _editingGradeColumnId = 0;
        GradeColumnName = string.Empty;

        // Suggests whatever is still missing from 100%, which is almost always
        // what the user is about to type.
        var remaining = 100m - (GradeStructureValidation?.TotalWeightPercent ?? 0m);
        GradeColumnWeightPercent = remaining > 0 ? remaining : 10m;

        GradeColumnMinScore = null;
        _editingGradeColumnPartCount = 1;
        DetailErrorMessage = null;
        IsGradeColumnEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditGradeColumn(AssessmentDto? column)
    {
        var target = column ?? SelectedGradeColumn;
        if (target is null)
        {
            return;
        }

        _editingGradeColumnId = target.AssessmentId;
        GradeColumnName = target.Name;
        GradeColumnWeightPercent = target.WeightPercent;
        GradeColumnMinScore = target.MinScoreToPass;
        _editingGradeColumnPartCount = target.PartCount;
        DetailErrorMessage = null;
        IsGradeColumnEditorOpen = true;
    }

    [RelayCommand]
    private void CloseGradeColumnEditor() => IsGradeColumnEditorOpen = false;

    [RelayCommand]
    private async Task SaveGradeColumnAsync()
    {
        if (DetailCourse is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GradeColumnName))
        {
            DetailErrorMessage = "* Tên cột điểm không được để trống.";
            return;
        }

        if (GradeColumnWeightPercent <= 0m || GradeColumnWeightPercent > 100m)
        {
            DetailErrorMessage = "* Trọng số phải lớn hơn 0% và không vượt quá 100%.";
            return;
        }

        var dto = new AssessmentDto(
            _editingGradeColumnId,
            DetailCourse.CourseId,
            GradeColumnName.Trim(),
            Math.Round(GradeColumnWeightPercent / 100m, CatalogRules.AssessmentWeightDecimals,
                MidpointRounding.AwayFromZero),
            GradeColumnMinScore,
            DisplayOrder: _editingGradeColumnId == 0 ? GradeColumns.Count : SelectedGradeColumn?.DisplayOrder ?? 0,
            // Carried through untouched - see the note on the backing field.
            PartCount: _editingGradeColumnPartCount);

        // allowUnbalanced stays true here: a structure is built one column at a
        // time, so demanding 100% on every save would make the first column
        // impossible to add. The banner reports the imbalance instead.
        await RunDetailActionAsync(
            () => _editingGradeColumnId == 0
                ? _gradeStructureService.CreateAsync(dto, allowUnbalanced: true)
                : _gradeStructureService.UpdateAsync(dto, allowUnbalanced: true),
            _editingGradeColumnId == 0 ? "Đã thêm cột điểm." : "Đã cập nhật cột điểm.");

        if (!HasDetailError)
        {
            IsGradeColumnEditorOpen = false;
        }
    }

    [RelayCommand]
    private async Task DeleteGradeColumnAsync(AssessmentDto? column)
    {
        var target = column ?? SelectedGradeColumn;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Xóa cột điểm '{target.Name}' ({target.WeightPercent}%)?\n\n" +
            "Không thể xóa nếu đã có điểm thành phần được ghi nhận.",
            "Xác nhận xóa cột điểm",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunDetailActionAsync(
            () => _gradeStructureService.DeleteAsync(target.AssessmentId), "Đã xóa cột điểm.");
    }

    // =========================================================================
    // Assessment schedule
    // =========================================================================

    [RelayCommand]
    private void OpenCreateScheduleItem()
    {
        _editingScheduleId = 0;
        ScheduleSessionNo = ScheduleItems.Count == 0 ? 1 : ScheduleItems.Max(s => s.SessionNo) + 1;
        ScheduleTitle = string.Empty;
        ScheduleDescription = null;
        ScheduleExpectedDate = null;
        ScheduleTeachingType = null;
        DetailErrorMessage = null;
        IsScheduleEditorOpen = true;
    }

    [RelayCommand]
    private void OpenEditScheduleItem(AssessmentScheduleDto? item)
    {
        var target = item ?? SelectedScheduleItem;
        if (target is null)
        {
            return;
        }

        _editingScheduleId = target.AssessmentScheduleId;
        ScheduleSessionNo = target.SessionNo;
        ScheduleTitle = target.Title;
        ScheduleDescription = target.Description;
        ScheduleExpectedDate = target.ExpectedDate;
        ScheduleTeachingType = target.TeachingType;
        DetailErrorMessage = null;
        IsScheduleEditorOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleEditor() => IsScheduleEditorOpen = false;

    [RelayCommand]
    private async Task SaveScheduleItemAsync()
    {
        if (DetailCourse is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ScheduleTitle))
        {
            DetailErrorMessage = "* Nội dung kiểm tra không được để trống.";
            return;
        }

        if (ScheduleSessionNo < 1)
        {
            DetailErrorMessage = "* Số buổi phải lớn hơn hoặc bằng 1.";
            return;
        }

        var dto = new AssessmentScheduleDto(
            _editingScheduleId,
            DetailCourse.CourseId,
            ScheduleSessionNo,
            // Left null so the service derives it from the session number,
            // keeping one rule for how a week is computed.
            WeekNo: null,
            ScheduleTitle.Trim(),
            string.IsNullOrWhiteSpace(ScheduleDescription) ? null : ScheduleDescription.Trim(),
            ScheduleExpectedDate,
            string.IsNullOrWhiteSpace(ScheduleTeachingType) ? null : ScheduleTeachingType.Trim(),
            AssessmentId: null,
            AssessmentName: null);

        await RunDetailActionAsync(
            () => _editingScheduleId == 0
                ? _scheduleService.CreateAsync(dto)
                : _scheduleService.UpdateAsync(dto),
            _editingScheduleId == 0 ? "Đã thêm lịch kiểm tra." : "Đã cập nhật lịch kiểm tra.");

        if (!HasDetailError)
        {
            IsScheduleEditorOpen = false;
        }
    }

    [RelayCommand]
    private async Task DeleteScheduleItemAsync(AssessmentScheduleDto? item)
    {
        var target = item ?? SelectedScheduleItem;
        if (target is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Xóa lịch kiểm tra buổi {target.SessionNo} - '{target.Title}'?",
            "Xác nhận xóa lịch kiểm tra",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunDetailActionAsync(
            () => _scheduleService.DeleteAsync(target.AssessmentScheduleId), "Đã xóa lịch kiểm tra.");
    }
}
