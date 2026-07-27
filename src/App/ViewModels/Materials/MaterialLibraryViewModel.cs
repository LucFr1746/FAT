using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Materials;

/// <summary>One subject's materials, rendered as a card with a header and rows.</summary>
public sealed record MaterialSubjectGroup(string SubjectDisplay, IReadOnlyList<MaterialLibraryItemDto> Items)
{
    public int Count => Items.Count;
}

/// <summary>
/// The material library - Member 5's module.
///
/// The text box searches by subject (code or name) and by material title; the
/// dropdowns are the coarse filters: term for everyone, and major for admins
/// (a student is locked to their own major). Rows are FLM links (click opens the
/// URL) or admin-uploaded files (click saves the file). Admins also upload here.
/// </summary>
public partial class MaterialLibraryViewModel : ViewModelBase, INavigationAware
{
    private readonly IMaterialLibraryService _library;
    private readonly IMaterialService _materials;
    private readonly ICurrentUserContext _currentUser;

    /// <summary>Results grouped by subject: one card per môn học with its rows inside.</summary>
    [ObservableProperty]
    private ObservableCollection<MaterialSubjectGroup> _groups = [];

    private int _resultCount;

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private ObservableCollection<MaterialMajorOptionDto> _majors = [];

    [ObservableProperty]
    private MaterialMajorOptionDto? _selectedMajor;

    [ObservableProperty]
    private ObservableCollection<MaterialTermOptionDto> _terms = [];

    [ObservableProperty]
    private MaterialTermOptionDto? _selectedTerm;

    [ObservableProperty]
    private bool _onlyDownloadable;

    [ObservableProperty]
    private string? _statusMessage;

    // Set while filter dropdowns are being seeded so the initial selection does
    // not trigger a search before OnNavigatedToAsync runs the first one.
    private bool _suppressFilterReload;

    // ----- Upload panel (admin only) -----

    [ObservableProperty]
    private ObservableCollection<MaterialSubjectOptionDto> _uploadSubjects = [];

    [ObservableProperty]
    private bool _isUploadPanelOpen;

    [ObservableProperty]
    private string? _uploadFilePath;

    [ObservableProperty]
    private string _uploadTitle = string.Empty;

    [ObservableProperty]
    private string _uploadCategory = MaterialCategories.Other;

    [ObservableProperty]
    private MaterialSubjectOptionDto? _uploadSubject;

    public ObservableCollection<string> Categories { get; } = new(MaterialCategories.All);

    /// <summary>Only admins pick a major and upload; a student is locked to their own major.</summary>
    public bool IsAdmin => _currentUser.IsAdmin;

    public bool ShowMajorFilter => _currentUser.IsAdmin;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public bool HasUploadFile => !string.IsNullOrWhiteSpace(UploadFilePath);

    partial void OnUploadFilePathChanged(string? value) => OnPropertyChanged(nameof(HasUploadFile));

    /// <summary>True after a search that returned nothing, to show the empty state.</summary>
    public bool HasNoResults => !IsBusy && _resultCount == 0;

    public MaterialLibraryViewModel(
        IMaterialLibraryService library,
        IMaterialService materials,
        ICurrentUserContext currentUser)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "Tài Liệu Học Tập";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        _suppressFilterReload = true;

        Terms.Clear();
        Terms.Add(MaterialTermOptionDto.All);
        for (var n = 0; n <= 9; n++)
        {
            Terms.Add(new MaterialTermOptionDto(n, $"Kỳ {n}"));
        }
        SelectedTerm = MaterialTermOptionDto.All;

        Majors.Clear();
        Majors.Add(MaterialMajorOptionDto.All);
        if (_currentUser.IsAdmin)
        {
            foreach (var major in await _library.GetMajorOptionsAsync(cancellationToken))
            {
                Majors.Add(major);
            }

            // The upload target list: every subject, so an admin can attach a file
            // to any course.
            foreach (var subject in await _library.GetSubjectOptionsAsync(null, cancellationToken))
            {
                UploadSubjects.Add(subject);
            }
        }
        SelectedMajor = MaterialMajorOptionDto.All;

        _suppressFilterReload = false;

        await SearchAsync(cancellationToken);
    }

    partial void OnSelectedMajorChanged(MaterialMajorOptionDto? value)
    {
        if (!_suppressFilterReload)
        {
            _ = SearchAsync();
        }
    }

    partial void OnSelectedTermChanged(MaterialTermOptionDto? value)
    {
        if (!_suppressFilterReload)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            var filter = new MaterialLibraryFilter(
                Keyword: Keyword,
                CourseId: null,
                OnlyDownloadable: OnlyDownloadable,
                MajorId: SelectedMajor?.MajorId,
                TermNo: SelectedTerm?.TermNo);

            var results = await _library.SearchAsync(filter, cancellationToken);

            // Group by subject, preserving the service's order (already sorted by
            // course code), so each subject becomes one card with its rows inside.
            Groups.Clear();
            foreach (var group in results.GroupBy(r => r.SubjectDisplay))
            {
                Groups.Add(new MaterialSubjectGroup(group.Key, group.ToList()));
            }

            _resultCount = results.Count;
            StatusMessage = $"Tìm thấy {_resultCount} tài liệu.";
            OnPropertyChanged(nameof(HasNoResults));
        });
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        Keyword = null;
        OnlyDownloadable = false;

        _suppressFilterReload = true;
        SelectedMajor = Majors.FirstOrDefault(m => m.IsAll);
        SelectedTerm = Terms.FirstOrDefault(t => t.IsAll);
        _suppressFilterReload = false;

        await SearchAsync();
    }

    /// <summary>
    /// A link row opens its URL in the browser; an uploaded row fetches the bytes
    /// and saves them where the user chooses.
    /// </summary>
    [RelayCommand]
    private async Task DownloadAsync(MaterialLibraryItemDto? material)
    {
        if (material is null)
        {
            return;
        }

        if (material.IsUploadedFile)
        {
            await RunBusyAsync(async () =>
            {
                var file = await _materials.DownloadAsync(material.UploadedMaterialId!.Value);
                if (file is null)
                {
                    ErrorMessage = "Không tải được tệp (tệp không còn tồn tại).";
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Lưu tài liệu",
                    FileName = file.FileName
                };

                if (dialog.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, file.Content);
                    StatusMessage = $"Đã lưu: {Path.GetFileName(dialog.FileName)}";
                }
            });
            return;
        }

        if (material.HasLink)
        {
            try
            {
                Process.Start(new ProcessStartInfo(material.Url!) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không mở được liên kết tài liệu: {ex.Message}";
            }
        }
    }

    // ----- Upload (admin) -----

    [RelayCommand]
    private void OpenUploadPanel()
    {
        UploadFilePath = null;
        UploadTitle = string.Empty;
        UploadCategory = MaterialCategories.Other;
        UploadSubject = UploadSubjects.FirstOrDefault();
        ErrorMessage = null;
        IsUploadPanelOpen = true;
    }

    [RelayCommand]
    private void CloseUploadPanel() => IsUploadPanelOpen = false;

    [RelayCommand]
    private void PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp tài liệu",
            Filter = "Tất cả tệp (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            UploadFilePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(UploadTitle))
            {
                UploadTitle = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (string.IsNullOrWhiteSpace(UploadFilePath))
        {
            ErrorMessage = "Hãy chọn tệp cần tải lên.";
            return;
        }

        if (UploadSubject is null)
        {
            ErrorMessage = "Hãy chọn môn học cho tài liệu.";
            return;
        }

        if (string.IsNullOrWhiteSpace(UploadTitle))
        {
            ErrorMessage = "Hãy nhập tiêu đề tài liệu.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var bytes = await File.ReadAllBytesAsync(UploadFilePath);
            var request = new MaterialUploadRequest(
                CourseId: UploadSubject.CourseId,
                Title: UploadTitle.Trim(),
                Description: null,
                Category: UploadCategory,
                FileName: Path.GetFileName(UploadFilePath),
                ContentType: "application/octet-stream",
                Content: bytes);

            await _materials.UploadAsync(request, _currentUser.User!.UserId);
            StatusMessage = $"Đã tải lên: {request.FileName}";
            IsUploadPanelOpen = false;
        });

        await SearchAsync();
    }
}
