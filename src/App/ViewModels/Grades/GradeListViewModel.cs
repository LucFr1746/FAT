using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

public sealed record GradeSemesterOption(int? SemesterId, string Display);

/// <summary>Read-only list of the signed-in student's course and component grades.</summary>
public partial class GradeListViewModel : ViewModelBase, INavigationAware
{
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserContext _currentUser;
    private IReadOnlyList<GradeCourseDto> _allGrades = [];

    [ObservableProperty]
    private ObservableCollection<GradeCourseDto> _grades = [];

    [ObservableProperty]
    private ObservableCollection<GradeSemesterOption> _semesters = [];

    [ObservableProperty]
    private GradeSemesterOption? _selectedSemester;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool IsEmpty => !IsBusy && !HasError && Grades.Count == 0;

    public GradeListViewModel(IGradeService gradeService, ICurrentUserContext currentUser)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "View Grades";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnSelectedSemesterChanged(GradeSemesterOption? value) => ApplyFilters();

    partial void OnSearchKeywordChanged(string value) => ApplyFilters();

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        var selectedSemesterId = SelectedSemester?.SemesterId;

        await RunBusyAsync(async () =>
        {
            _allGrades = await _gradeService.GetStudentGradesAsync(studentId, cancellationToken);

            Semesters.Clear();
            Semesters.Add(new GradeSemesterOption(null, "Tất cả học kỳ"));
            foreach (var semester in _allGrades
                         .GroupBy(g => new { g.SemesterId, g.SemesterCode, g.SemesterDisplayOrder })
                         .OrderByDescending(g => g.Key.SemesterDisplayOrder))
            {
                Semesters.Add(new GradeSemesterOption(
                    semester.Key.SemesterId,
                    semester.Key.SemesterCode));
            }

            SelectedSemester = Semesters.FirstOrDefault(s => s.SemesterId == selectedSemesterId)
                               ?? Semesters.FirstOrDefault();
            ApplyFilters();
            StatusMessage = $"Đã tải {_allGrades.Count} lượt học.";
        });

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void ApplyFilters()
    {
        var keyword = SearchKeyword?.Trim();
        var query = _allGrades.AsEnumerable();

        if (SelectedSemester?.SemesterId is int semesterId)
        {
            query = query.Where(g => g.SemesterId == semesterId);
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

        OnPropertyChanged(nameof(IsEmpty));
    }
}
