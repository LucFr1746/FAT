using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

/// <summary>Adds, edits and deletes scores for assessments that already exist.</summary>
public partial class GradeEntryViewModel : ViewModelBase, INavigationAware
{
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserContext _currentUser;
    private IReadOnlyList<GradeCourseDto> _allCourses = [];

    [ObservableProperty]
    private ObservableCollection<GradeTermOptionDto> _terms = [];

    [ObservableProperty]
    private GradeTermOptionDto? _selectedTerm;

    [ObservableProperty]
    private ObservableCollection<GradeCourseDto> _courses = [];

    [ObservableProperty]
    private GradeCourseDto? _selectedCourse;

    [ObservableProperty]
    private ObservableCollection<GradeAssessmentDto> _assessments = [];

    [ObservableProperty]
    private GradeAssessmentDto? _selectedAssessment;

    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasCourses => Courses.Count > 0;
    public bool HasAssessments => Assessments.Count > 0;
    public bool CanDeleteSelected => SelectedAssessment?.HasScore == true;
    public string EditModeLabel => SelectedAssessment?.HasScore == true ? "Chỉnh sửa điểm" : "Thêm điểm";

    public GradeEntryViewModel(
        IGradeService gradeService,
        ICurrentUserContext currentUser)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "Nhập điểm";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnSelectedTermChanged(GradeTermOptionDto? value)
        => ApplyTermFilter();

    partial void OnSelectedCourseChanged(GradeCourseDto? value)
    {
        Assessments.Clear();
        if (value is not null)
        {
            foreach (var assessment in value.Assessments)
            {
                Assessments.Add(assessment);
            }
        }

        SelectedAssessment = Assessments.FirstOrDefault();
        OnPropertyChanged(nameof(HasAssessments));
    }

    partial void OnSelectedAssessmentChanged(GradeAssessmentDto? value)
    {
        ScoreText = value?.Score?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ValidationMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(EditModeLabel));
    }

    partial void OnValidationMessageChanged(string? value) => OnPropertyChanged(nameof(HasValidationError));

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
        => await LoadAsync(
            SelectedCourse?.EnrollmentId,
            SelectedAssessment?.AssessmentId,
            cancellationToken);

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ValidationMessage = null;
        StatusMessage = null;

        if (SelectedCourse is null)
        {
            ValidationMessage = "Môn học không được để trống.";
            return;
        }

        if (_currentUser.StudentId is not int studentId)
        {
            ValidationMessage = "Không tìm thấy tài khoản sinh viên đang đăng nhập.";
            return;
        }

        if (SelectedAssessment is null)
        {
            ValidationMessage = "Assessment không được để trống.";
            return;
        }

        if (!TryParseScore(ScoreText, out var score))
        {
            ValidationMessage = "Điểm phải là một số hợp lệ.";
            return;
        }

        if (score < 0m)
        {
            ValidationMessage = "Điểm không được nhỏ hơn 0.";
            return;
        }

        if (score > GradeAssessmentDto.MaxScore)
        {
            ValidationMessage =
                $"Điểm không được lớn hơn điểm tối đa {GradeAssessmentDto.MaxScore:0}.";
            return;
        }

        var enrollmentId = SelectedCourse.EnrollmentId;
        var courseId = SelectedCourse.CourseId;
        var assessmentId = SelectedAssessment.AssessmentId;

        await RunBusyAsync(async () =>
        {
            enrollmentId = await _gradeService.UpsertStudentGradeAsync(
                studentId,
                enrollmentId,
                courseId,
                assessmentId,
                score,
                cancellationToken);
            StatusMessage = "Đã lưu điểm và tính lại điểm tổng kết.";
        });

        if (!HasError)
        {
            await LoadAsync(enrollmentId, assessmentId, cancellationToken, preserveStatus: true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        ValidationMessage = null;
        StatusMessage = null;

        if (SelectedCourse is null || SelectedAssessment?.HasScore != true)
        {
            ValidationMessage = "Hãy chọn một điểm đã có để xóa.";
            return;
        }

        var confirmed = MessageBox.Show(
            $"Xóa điểm của assessment '{SelectedAssessment.Name}' " +
            $"trong môn {SelectedCourse.CourseCode}?",
            "Xác nhận xóa điểm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        var enrollmentId = SelectedCourse.EnrollmentId;
        var assessmentId = SelectedAssessment.AssessmentId;

        await RunBusyAsync(async () =>
        {
            await _gradeService.DeleteGradeAsync(
                enrollmentId, assessmentId, cancellationToken);
            StatusMessage = "Đã xóa điểm và cập nhật lại trạng thái môn học.";
        });

        if (!HasError)
        {
            await LoadAsync(enrollmentId, assessmentId, cancellationToken, preserveStatus: true);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ScoreText = SelectedAssessment?.Score?.ToString(
            "0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ValidationMessage = null;
        StatusMessage = "Đã hủy nội dung đang nhập.";
    }

    private async Task LoadAsync(
        int? enrollmentId,
        int? assessmentId,
        CancellationToken cancellationToken,
        bool preserveStatus = false)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        var existingStatus = StatusMessage;

        await RunBusyAsync(async () =>
        {
            _allCourses = (await _gradeService.GetStudentGradesAsync(
                    studentId, cancellationToken))
                .Where(c => c.CanManageGrades)
                .ToList();

            var termOptions = await _gradeService.GetTermOptionsAsync(studentId, cancellationToken);
            var selectedTermNo = SelectedTerm?.TermNo;
            Terms.Clear();
            Terms.Add(GradeTermOptionDto.All);
            foreach (var term in termOptions)
            {
                Terms.Add(term);
            }

            SelectedTerm = Terms.FirstOrDefault(t => t.TermNo == selectedTermNo)
                           ?? Terms.FirstOrDefault();
            ApplyTermFilter(enrollmentId);

            SelectedAssessment = Assessments.FirstOrDefault(a => a.AssessmentId == assessmentId)
                                 ?? Assessments.FirstOrDefault();

            ValidationMessage = null;
            StatusMessage = preserveStatus
                ? existingStatus
                : $"Đã tải {_allCourses.Select(c => c.CourseId).Distinct().Count()} môn " +
                  $"thuộc {termOptions.Count} học kỳ.";
            OnPropertyChanged(nameof(HasCourses));
            OnPropertyChanged(nameof(HasAssessments));
        });
    }

    private void ApplyTermFilter(int? preferredEnrollmentId = null)
    {
        var previousCourseId = SelectedCourse?.CourseId;
        var query = _allCourses.AsEnumerable();

        if (SelectedTerm?.TermNo is int termNo)
        {
            query = query.Where(c => c.CurriculumTermNo == termNo);
        }

        Courses.Clear();
        foreach (var course in query)
        {
            Courses.Add(course);
        }

        SelectedCourse = Courses.FirstOrDefault(c =>
                             preferredEnrollmentId.HasValue
                             && c.EnrollmentId == preferredEnrollmentId.Value)
                         ?? Courses.FirstOrDefault(c => c.CourseId == previousCourseId)
                         ?? Courses.FirstOrDefault();

        OnPropertyChanged(nameof(HasCourses));
    }

    private static bool TryParseScore(string? text, out decimal score)
        => decimal.TryParse(
               text,
               NumberStyles.Number,
               CultureInfo.CurrentCulture,
               out score)
           || decimal.TryParse(
               text,
               NumberStyles.Number,
               CultureInfo.InvariantCulture,
               out score);
}
