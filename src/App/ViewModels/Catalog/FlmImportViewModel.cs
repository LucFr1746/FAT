using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.Navigation;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Catalog;

/// <summary>
/// Imports the FLM catalog from the Excel workbook or the JSON folder.
///
/// The screen insists on a PREVIEW before the import: this touches the entire
/// catalog and there is no undo, so the administrator sees what is about to
/// happen and any warnings first.
/// </summary>
public partial class FlmImportViewModel : ViewModelBase, INavigationAware
{
    private readonly IFlmImportService _importService;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private ImportPreviewDto? _preview;

    [ObservableProperty]
    private ImportResultDto? _result;

    [ObservableProperty]
    private ObservableCollection<string> _warnings = [];

    /// <summary>
    /// Whether there is anything to show in the warnings card.
    ///
    /// A bool rather than binding Visibility to Warnings.Count:
    /// BooleanToVisibilityConverter takes a bool, and handing it an int makes
    /// the card either always visible or always hidden.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    [ObservableProperty]
    private bool _updateExisting = true;

    [ObservableProperty]
    private bool _importMaterials = true;

    [ObservableProperty]
    private bool _importAssessments = true;

    [ObservableProperty]
    private bool _importSchedules = true;

    [ObservableProperty]
    private bool _importPrerequisites = true;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public bool HasPreview => Preview is not null;

    partial void OnPreviewChanged(ImportPreviewDto? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(CanImport));
    }

    public bool HasResult => Result is not null;

    partial void OnResultChanged(ImportResultDto? value) => OnPropertyChanged(nameof(HasResult));

    /// <summary>Import stays disabled until a preview has shown there is data.</summary>
    public bool CanImport => Preview is not null && Preview.HasData;

    public FlmImportViewModel(IFlmImportService importService)
    {
        _importService = importService;
        Title = "Import Dữ Liệu Chương Trình Học (FLM)";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        // Defaults to the copy shipped next to the executable, so the common
        // case is two clicks rather than a hunt through the filesystem.
        var bundled = Path.Combine(AppContext.BaseDirectory, "Data", "flm_chuong_trinh_hoc.xlsx");

        if (File.Exists(bundled))
        {
            FilePath = bundled;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void BrowseWorkbook()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn tệp Excel chương trình học",
            Filter = "Excel (*.xlsx)|*.xlsx|Tất cả tệp (*.*)|*.*",
            InitialDirectory = Path.Combine(AppContext.BaseDirectory, "Data")
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            ResetOutcome();
        }
    }

    /// <summary>
    /// Picks the JSON folder.
    ///
    /// OpenFileDialog rather than a folder browser: WPF has no built-in folder
    /// picker, and the JSON reader accepts any .json inside the folder anyway, so
    /// selecting one file is equivalent and needs no extra dependency.
    /// </summary>
    [RelayCommand]
    private void BrowseJsonFolder()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn một tệp JSON bất kỳ trong thư mục dữ liệu",
            Filter = "JSON (*.json)|*.json",
            InitialDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "json")
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = Path.GetDirectoryName(dialog.FileName);
            ResetOutcome();
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ErrorMessage = "Hãy chọn tệp Excel hoặc thư mục JSON trước.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            ResetOutcome();

            Preview = await _importService.PreviewAsync(FilePath);

            ReplaceWarnings(Preview.Warnings);

            StatusMessage = Preview.HasData
                ? $"Đã đọc {Preview.SubjectCount} môn học từ {Preview.SourceName}. Kiểm tra rồi bấm Import."
                : "Tệp không chứa dữ liệu môn học nào.";
        });
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || Preview is null)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Import {Preview.SubjectCount} môn học của {Preview.MajorCount} ngành từ:\n{FilePath}\n\n" +
            (UpdateExisting
                ? "Dữ liệu đã tồn tại sẽ được CẬP NHẬT (không tạo bản sao)."
                : "Dữ liệu đã tồn tại sẽ được GIỮ NGUYÊN, chỉ thêm mới.") +
            "\n\nBạn có chắc chắn muốn tiếp tục?",
            "Xác nhận import dữ liệu",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;

            var options = new ImportOptions(
                UpdateExisting, ImportMaterials, ImportAssessments, ImportSchedules, ImportPrerequisites);

            Result = await _importService.ImportAsync(FilePath, options);

            ReplaceWarnings(Result.Warnings);

            if (!Result.IsSuccess)
            {
                ErrorMessage = string.Join(" ", Result.Errors);
                return;
            }

            StatusMessage =
                $"Import hoàn tất trong {Result.Duration.TotalSeconds:0.#}s. " +
                $"Tạo mới {Result.TotalCreated}, cập nhật {Result.TotalUpdated}.";
        });
    }

    private void ResetOutcome()
    {
        Preview = null;
        Result = null;
        ReplaceWarnings([]);
        StatusMessage = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Swaps the warning list and tells the view.
    ///
    /// The explicit notification is required: HasWarnings is derived from
    /// Warnings.Count, and mutating an ObservableCollection raises
    /// CollectionChanged, not a PropertyChanged for a computed property on the
    /// view model - so the card would never appear or never disappear.
    /// </summary>
    private void ReplaceWarnings(IReadOnlyList<string> warnings)
    {
        Warnings.Clear();
        foreach (var warning in warnings)
        {
            Warnings.Add(warning);
        }

        OnPropertyChanged(nameof(HasWarnings));
    }
}
