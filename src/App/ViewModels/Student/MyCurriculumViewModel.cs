using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Constants;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Student;

/// <summary>
/// The student's own curriculum: pick a programme, pick a kỳ, see the subjects.
///
/// Backs the workflow
///   Login -&gt; chọn Major -&gt; chọn Kỳ hiện tại -&gt; danh sách môn
/// with each subject showing its credits, GPA flag, grade structure, materials,
/// timeline and prerequisites, plus the Add Retake Subject action.
///
/// Subjects with unmet prerequisites are HIDDEN, per the rule. The screen says
/// how many were hidden so the shorter list does not read as missing data.
/// </summary>
public partial class MyCurriculumViewModel : ViewModelBase, INavigationAware
{
    private readonly IStudentCurriculumService _studentCurriculum;
    private readonly ICourseService _courseService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMaterialService _materialService;

    [ObservableProperty]
    private ObservableCollection<MajorDto> _majors = [];

    [ObservableProperty]
    private MajorDto? _selectedMajor;

    [ObservableProperty]
    private ObservableCollection<int> _termNumbers = [];

    [ObservableProperty]
    private int _selectedTermNo;

    [ObservableProperty]
    private StudentTermCurriculumDto? _curriculum;

    [ObservableProperty]
    private ObservableCollection<StudentSubjectDto> _subjects = [];

    [ObservableProperty]
    private StudentSubjectDetailDto? _subjectDetail;

    [ObservableProperty]
    private string? _statusMessage;

    // ----- Retake -----
    [ObservableProperty]
    private ObservableCollection<RetakeCandidateDto> _retakeCandidates = [];

    [ObservableProperty]
    private RetakeCandidateDto? _selectedRetakeCandidate;

    [ObservableProperty]
    private ObservableCollection<SemesterDto> _semesters = [];

    [ObservableProperty]
    private SemesterDto? _selectedRetakeSemester;

    [ObservableProperty]
    private bool _isRetakeDialogOpen;

    [ObservableProperty]
    private string? _retakeErrorMessage;

    public bool HasRetakeError => !string.IsNullOrWhiteSpace(RetakeErrorMessage);

    partial void OnRetakeErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasRetakeError));

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public bool IsDetailOpen => SubjectDetail is not null;

    partial void OnSubjectDetailChanged(StudentSubjectDetailDto? value) => OnPropertyChanged(nameof(IsDetailOpen));

    public bool HasHiddenSubjects => Curriculum?.HasHiddenSubjects ?? false;

    /// <summary>Explains the shorter list rather than leaving a silent gap in it.</summary>
    public string HiddenSubjectsMessage => Curriculum is null || !Curriculum.HasHiddenSubjects
        ? string.Empty
        : $"{Curriculum.HiddenByPrerequisiteCount} môn đang được ẩn do chưa đạt môn tiên quyết: " +
          string.Join(", ", Curriculum.HiddenSubjectCodes);

    public bool IsEmpty => !IsBusy && FilteredSubjects.Count == 0 && !HasError;

    public MyCurriculumViewModel(
        IStudentCurriculumService studentCurriculum,
        ICourseService courseService,
        ICurrentUserContext currentUser,
        IMaterialService materialService)
    {
        _studentCurriculum = studentCurriculum;
        _courseService = courseService;
        _currentUser = currentUser;
        _materialService = materialService;
        Title = "Chương Trình Học Của Tôi";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        await RunBusyAsync(async () =>
        {
            try
            {
                var majors = await _courseService.GetMajorsAsync(cancellationToken);
                Majors.Clear();
                foreach (var major in majors)
                {
                    Majors.Add(major);
                }

                var semesters = await _courseService.GetSemestersAsync(cancellationToken);
                Semesters.Clear();
                foreach (var semester in semesters)
                {
                    Semesters.Add(semester);
                }

                // Defaults to the current semester, which is where a retake belongs.
                SelectedRetakeSemester = Semesters.FirstOrDefault(s => s.IsCurrent) ?? Semesters.LastOrDefault();

                TermNumbers.Clear();
                for (var termNo = CatalogRules.MinTermNo; termNo <= 9; termNo++)
                {
                    TermNumbers.Add(termNo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MyCurriculumViewModel OnNavigatedToAsync error] {ex}");
                ErrorMessage = $"Lỗi khởi tạo màn hình chương trình học: {ex.Message}";
            }
        });

        await LoadCurriculumAsync(cancellationToken);
    }

    [ObservableProperty]
    private CurriculumProgressDto? _progress;

    public string ProgressText => Progress == null
        ? "0 / 0 môn (0 / 0 tín chỉ)"
        : $"{Progress.CompletedSubjects} / {Progress.TotalSubjects} môn ({Progress.CompletedCredits} / {Progress.TotalCredits} tín chỉ)";

    public double ProgressPercentage => Progress?.ProgressPercentage ?? 0.0;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<StudentSubjectDto> _filteredSubjects = [];

    partial void OnProgressChanged(CurriculumProgressDto? value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercentage));
    }

    partial void OnSearchKeywordChanged(string value)
    {
        FilterSubjects();
    }

    private void FilterSubjects()
    {
        FilteredSubjects.Clear();
        var query = SearchKeyword?.Trim();
        foreach (var s in Subjects)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                s.CourseCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.CourseName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredSubjects.Add(s);
            }
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Loads the kỳ the student is currently in.
    ///
    /// The student's own major and kỳ are read from their profile rather than
    /// passed in as navigation parameters - a screen that decides whose data it
    /// shows is one wrong argument away from exposing another student's record.
    /// </summary>
    [RelayCommand]
    private async Task LoadCurriculumAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            try
            {
                StatusMessage = null;

                var curriculum = await _studentCurriculum.GetTermCurriculumAsync(
                    studentId, SelectedTermNo, cancellationToken);

                Curriculum = curriculum;
                Progress = curriculum.Progress;
                SelectedMajor = Majors.FirstOrDefault(m => m.MajorId == curriculum.MajorId);
                SelectedTermNo = curriculum.TermNo;

                Subjects.Clear();
                foreach (var subject in curriculum.Subjects)
                {
                    Subjects.Add(subject);
                }

                FilterSubjects();

                OnPropertyChanged(nameof(HasHiddenSubjects));
                OnPropertyChanged(nameof(HiddenSubjectsMessage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MyCurriculumViewModel LoadCurriculumAsync error] {ex}");
                ErrorMessage = $"Không thể tải chương trình học: {ex.Message}";
            }
        });

        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task ChangeMajorAsync()
    {
        if (SelectedMajor is null || _currentUser.StudentId is not int studentId)
        {
            return;
        }

        if (Curriculum is not null && Curriculum.MajorId == SelectedMajor.MajorId)
        {
            return;
        }

        var confirmed = System.Windows.MessageBox.Show(
            $"Chuyển sang ngành '{SelectedMajor.MajorCode} - {SelectedMajor.MajorName}'?\n\n" +
            "Kỳ học hiện tại sẽ được đặt lại vì thuộc chương trình học khác.",
            "Xác nhận đổi ngành",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirmed != System.Windows.MessageBoxResult.Yes)
        {
            // Put the picker back where it was rather than leaving it showing a
            // programme the student did not actually switch to.
            SelectedMajor = Majors.FirstOrDefault(m => m.MajorId == Curriculum?.MajorId);
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _studentCurriculum.SetMajorAsync(studentId, SelectedMajor.MajorId);
            SelectedTermNo = 0;
            StatusMessage = $"Đã chuyển sang ngành '{SelectedMajor.MajorCode}'.";
        });

        await LoadCurriculumAsync();
    }

    [RelayCommand]
    private async Task ChangeTermAsync()
    {
        if (_currentUser.StudentId is not int studentId)
        {
            return;
        }

        await RunBusyAsync(() => _studentCurriculum.SetCurrentTermAsync(studentId, SelectedTermNo));
        await LoadCurriculumAsync();
    }

    [RelayCommand]
    private async Task OpenSubjectDetailAsync(StudentSubjectDto? subject)
    {
        if (subject is null || _currentUser.StudentId is not int studentId)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            SubjectDetail = await _studentCurriculum.GetSubjectDetailAsync(studentId, subject.CourseId);
        });
    }

    [RelayCommand]
    private void CloseSubjectDetail() => SubjectDetail = null;

    /// <summary>Opens a material link in the browser or downloads an uploaded file.</summary>
    [RelayCommand]
    private async Task OpenMaterialUrlAsync(SubjectMaterialDto? material)
    {
        if (material is null)
        {
            return;
        }

        if (material.IsUploadedFile && material.MaterialId.HasValue)
        {
            await RunBusyAsync(async () =>
            {
                var file = await _materialService.DownloadAsync(material.MaterialId.Value);
                if (file is null)
                {
                    ErrorMessage = "Không tải được tệp (tệp không còn tồn tại).";
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Lưu tài liệu",
                    FileName = file.FileName
                };

                if (dialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllBytesAsync(dialog.FileName, file.Content);
                    StatusMessage = $"Đã lưu: {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            });
            return;
        }

        if (material.Url is null)
        {
            return;
        }

        if (!Uri.TryCreate(material.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "Đường dẫn tài liệu không hợp lệ.";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không mở được đường dẫn: {ex.Message}";
        }
    }

    // =========================================================================
    // Retake
    // =========================================================================

    [RelayCommand]
    private async Task OpenRetakeDialogAsync()
    {
        if (_currentUser.StudentId is not int studentId)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            RetakeErrorMessage = null;

            var candidates = await _studentCurriculum.GetRetakeCandidatesAsync(studentId);

            RetakeCandidates.Clear();
            foreach (var candidate in candidates)
            {
                RetakeCandidates.Add(candidate);
            }

            SelectedRetakeCandidate = RetakeCandidates.FirstOrDefault();
            IsRetakeDialogOpen = true;

            if (RetakeCandidates.Count == 0)
            {
                RetakeErrorMessage = "Hiện không có môn nào cần học lại.";
            }
        });
    }

    [RelayCommand]
    private void CloseRetakeDialog()
    {
        IsRetakeDialogOpen = false;
        RetakeErrorMessage = null;
    }

    [RelayCommand]
    private async Task ConfirmRetakeAsync()
    {
        if (_currentUser.StudentId is not int studentId)
        {
            return;
        }

        RetakeErrorMessage = null;

        if (SelectedRetakeCandidate is null)
        {
            RetakeErrorMessage = "* Hãy chọn môn học cần học lại.";
            return;
        }

        if (SelectedRetakeSemester is null)
        {
            RetakeErrorMessage = "* Hãy chọn học kỳ để đăng ký học lại.";
            return;
        }

        try
        {
            await RunBusyAsync(async () =>
            {
                await _studentCurriculum.AddRetakeAsync(
                    studentId, SelectedRetakeCandidate.CourseId, SelectedRetakeSemester.SemesterId);

                StatusMessage =
                    $"Đã đăng ký học lại môn '{SelectedRetakeCandidate.CourseCode}' " +
                    $"trong học kỳ {SelectedRetakeSemester.SemesterCode}.";

                IsRetakeDialogOpen = false;
            });

            // RunBusyAsync captures the failure into ErrorMessage; move it into
            // the dialog so the message appears where the user is looking.
            if (HasError)
            {
                RetakeErrorMessage = ErrorMessage;
                ErrorMessage = null;
                IsRetakeDialogOpen = true;
                return;
            }

            await LoadCurriculumAsync();
        }
        catch (Exception ex)
        {
            RetakeErrorMessage = ex.Message;
        }
    }
}
