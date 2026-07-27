using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.GradeGpa;

public sealed partial class GradeListViewModel : StudentAcademicViewModelBase
{
    private readonly IGradeWorkspaceService _workspace;
    private IReadOnlyList<GradeCourseDto> _allCourses = [];

    public GradeListViewModel(
        IGradeWorkspaceService workspace,
        ICurrentUserContext currentUser)
        : base(currentUser)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Title = "View Grades";
    }

    public ObservableCollection<GradeCourseDto> Courses { get; } = [];
    public ObservableCollection<GradeSemesterOptionDto> Semesters { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private GradeSemesterOptionDto? _selectedSemester;

    public bool IsEmpty => Courses.Count == 0;

    protected override Task LoadAsync(CancellationToken cancellationToken)
        => RefreshCoreAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
        => RefreshCoreAsync(CancellationToken.None);

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSemesterChanged(GradeSemesterOptionDto? value) => ApplyFilters();

    private Task RefreshCoreAsync(CancellationToken cancellationToken)
        => RunBusyAsync(async () =>
        {
            _allCourses = await _workspace.GetStudentGradesAsync(StudentId, cancellationToken);

            Semesters.Clear();
            Semesters.Add(GradeSemesterOptionDto.All);

            foreach (var semester in _allCourses
                         .GroupBy(course => new
                         {
                             course.SemesterId,
                             course.SemesterCode,
                             course.SemesterName,
                             course.SemesterDisplayOrder
                         })
                         .OrderByDescending(group => group.Key.SemesterDisplayOrder))
            {
                var displayName = string.IsNullOrWhiteSpace(semester.Key.SemesterName)
                    ? semester.Key.SemesterCode
                    : $"{semester.Key.SemesterCode} — {semester.Key.SemesterName}";

                Semesters.Add(new GradeSemesterOptionDto(semester.Key.SemesterId, displayName));
            }

            SelectedSemester = Semesters[0];
            ApplyFilters();
        });

    private void ApplyFilters()
    {
        var keyword = SearchText.Trim();
        var selectedSemesterId = SelectedSemester?.SemesterId;

        var filtered = _allCourses.Where(course =>
            (!selectedSemesterId.HasValue || course.SemesterId == selectedSemesterId)
            && (string.IsNullOrEmpty(keyword)
                || course.CourseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || course.CourseName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        Courses.Clear();
        foreach (var course in filtered)
        {
            Courses.Add(course);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
