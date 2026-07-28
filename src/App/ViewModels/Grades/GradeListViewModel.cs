using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

/// <summary>Read-only list of the signed-in student's course and component grades.</summary>
public partial class GradeListViewModel : ViewModelBase, INavigationAware
{
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserContext _currentUser;
    private IReadOnlyList<GradeCourseDto> _allGrades = [];

    [ObservableProperty]
    private ObservableCollection<GradeCourseDto> _grades = [];

    [ObservableProperty]
    private GradeCourseDto? _selectedGrade;

    [ObservableProperty]
    private ObservableCollection<GradeTermOptionDto> _terms = [];

    [ObservableProperty]
    private GradeTermOptionDto? _selectedTerm;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool IsEmpty => !IsBusy && !HasError && Grades.Count == 0;
    public bool HasSelection => SelectedGrade is not null;

    public GradeListViewModel(
        IGradeService gradeService,
        ICurrentUserContext currentUser)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "Xem điểm";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnSelectedTermChanged(GradeTermOptionDto? value) => ApplyFilters();

    partial void OnSearchKeywordChanged(string value) => ApplyFilters();

    partial void OnSelectedGradeChanged(GradeCourseDto? value)
        => OnPropertyChanged(nameof(HasSelection));

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        var selectedTermNo = SelectedTerm?.TermNo;

        await RunBusyAsync(async () =>
        {
            _allGrades = await _gradeService.GetStudentGradesAsync(studentId, cancellationToken);
            var termOptions = await _gradeService.GetTermOptionsAsync(studentId, cancellationToken);

            Terms.Clear();
            Terms.Add(GradeTermOptionDto.All);
            foreach (var term in termOptions)
            {
                Terms.Add(term);
            }

            SelectedTerm = Terms.FirstOrDefault(t => t.TermNo == selectedTermNo)
                           ?? Terms.FirstOrDefault();
            ApplyFilters();

            var enrolledAttempts = _allGrades.Count(g => g.IsEnrolled);
            var curriculumCourses = _allGrades.Select(g => g.CourseId).Distinct().Count();
            StatusMessage =
                $"Đã tải {curriculumCourses} môn thuộc {termOptions.Count} học kỳ " +
                $"({enrolledAttempts} lượt học đã đăng ký).";
        });

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void ApplyFilters()
    {
        var selectedEnrollmentId = SelectedGrade?.EnrollmentId;
        var selectedCourseId = SelectedGrade?.CourseId;
        var keyword = SearchKeyword?.Trim();
        var query = _allGrades.AsEnumerable();

        if (SelectedTerm?.TermNo is int termNo)
        {
            query = query.Where(g => g.CurriculumTermNo == termNo);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(g =>
                g.CourseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || g.CourseName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        Grades.Clear();
        foreach (var grade in query)
        {
            Grades.Add(grade);
        }

        SelectedGrade = Grades.FirstOrDefault(g =>
                            selectedEnrollmentId > 0
                            && g.EnrollmentId == selectedEnrollmentId)
                        ?? Grades.FirstOrDefault(g => g.CourseId == selectedCourseId)
                        ?? Grades.FirstOrDefault();

        OnPropertyChanged(nameof(IsEmpty));
    }
}
