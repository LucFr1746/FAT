using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace FAT.App.ViewModels.GradeGpa;

public abstract partial class StudentScreenViewModel(ICurrentUserContext user) : ViewModelBase, INavigationAware
{
    protected int StudentId => user.RequireStudentId();
    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default) => LoadAsync(parameter, cancellationToken);
    protected abstract Task LoadAsync(object? parameter, CancellationToken cancellationToken);
}

public sealed partial class GradeListViewModel(IGradeService service, ICurrentUserContext user) : StudentScreenViewModel(user)
{
    public ObservableCollection<SemesterTranscriptDto> Semesters { get; } = [];
    protected override Task LoadAsync(object? parameter, CancellationToken token) => RunBusyAsync(async () =>
    { Semesters.Clear(); foreach (var row in (await service.GetTranscriptAsync(StudentId, token)).Semesters) Semesters.Add(row); });
}

public sealed partial class TranscriptViewModel(IGradeService service, ICurrentUserContext user) : StudentScreenViewModel(user)
{
    [ObservableProperty] private TranscriptDto? _transcript;
    protected override Task LoadAsync(object? parameter, CancellationToken token) => RunBusyAsync(async () => Transcript = await service.GetTranscriptAsync(StudentId, token));
}

public sealed partial class GpaCalculatorViewModel(IGpaService service, ICurrentUserContext user) : StudentScreenViewModel(user)
{
    [ObservableProperty] private GpaSummaryDto? _summary;
    [ObservableProperty] private CreditSummaryDto? _credits;
    protected override Task LoadAsync(object? parameter, CancellationToken token) => RunBusyAsync(async () =>
    { Summary = await service.GetGpaSummaryAsync(StudentId, token); Credits = await service.GetCreditSummaryAsync(StudentId, token); });
}

public sealed partial class StatisticsViewModel(IAnalyticsService service, ICurrentUserContext user) : StudentScreenViewModel(user)
{
    [ObservableProperty] private DashboardDto? _dashboard;
    [ObservableProperty] private IReadOnlyList<CourseHighlightDto> _topCourses = [];
    [ObservableProperty] private IReadOnlyList<CourseHighlightDto> _weakestCourses = [];
    [ObservableProperty] private ISeries[] _gpaSeries = [];
    [ObservableProperty] private ISeries[] _distributionSeries = [];
    [ObservableProperty] private Axis[] _gpaXAxes = [];
    [ObservableProperty] private Axis[] _distributionXAxes = [];
    protected override Task LoadAsync(object? parameter, CancellationToken token) => RunBusyAsync(async () =>
    {
        Dashboard = await service.GetDashboardAsync(StudentId, token);
        TopCourses = await service.GetTopCoursesAsync(StudentId, cancellationToken: token);
        WeakestCourses = await service.GetWeakestCoursesAsync(StudentId, cancellationToken: token);
        GpaSeries = [new LineSeries<double> { Name = "GPA", Values = Dashboard.GpaTrend.Select(x => (double)(x.Gpa ?? 0)).ToArray(), GeometrySize = 10 }];
        GpaXAxes = [new Axis { Labels = Dashboard.GpaTrend.Select(x => x.SemesterCode).ToArray() }];
        DistributionSeries = [new ColumnSeries<int> { Name = "Courses", Values = Dashboard.GradeDistribution.Select(x => x.Count).ToArray() }];
        DistributionXAxes = [new Axis { Labels = Dashboard.GradeDistribution.Select(x => x.LetterGrade).ToArray() }];
    });
}

public sealed partial class GradeEntryViewModel(IGradeService service, GradeWorkspaceService workspace) : ViewModelBase, INavigationAware
{
    public ObservableCollection<StudentOption> Students { get; } = [];
    public ObservableCollection<EnrollmentOption> Enrollments { get; } = [];
    public ObservableCollection<AssessmentScore> Assessments { get; } = [];
    [ObservableProperty] private StudentOption? _selectedStudent;
    [ObservableProperty] private EnrollmentOption? _selectedEnrollment;
    [ObservableProperty] private AssessmentScore? _selectedAssessment;
    [ObservableProperty] private decimal _score;
    [ObservableProperty] private string? _successMessage;

    public Task OnNavigatedToAsync(object? parameter, CancellationToken token = default) => RunBusyAsync(async () =>
    {
        Students.Clear(); foreach (var item in await workspace.GetStudentsAsync(token)) Students.Add(item);
    });

    partial void OnSelectedStudentChanged(StudentOption? value) => _ = LoadEnrollmentsAsync(value);
    partial void OnSelectedEnrollmentChanged(EnrollmentOption? value) => _ = LoadAssessmentsAsync(value);
    partial void OnSelectedAssessmentChanged(AssessmentScore? value) { if (value?.Score is decimal score) Score = score; }

    private Task LoadEnrollmentsAsync(StudentOption? student) => RunBusyAsync(async () =>
    { Enrollments.Clear(); Assessments.Clear(); if (student is not null) foreach (var item in await workspace.GetEnrollmentsAsync(student.StudentId)) Enrollments.Add(item); });
    private Task LoadAssessmentsAsync(EnrollmentOption? enrollment) => RunBusyAsync(async () =>
    { Assessments.Clear(); if (enrollment is not null) foreach (var item in await workspace.GetAssessmentScoresAsync(enrollment.EnrollmentId)) Assessments.Add(item); });

    [RelayCommand] private Task SaveAsync(CancellationToken token = default) => RunBusyAsync(async () =>
    {
        if (SelectedEnrollment is null || SelectedAssessment is null) throw new InvalidOperationException("Select a student, course and assessment first.");
        await service.UpsertGradeAsync(SelectedEnrollment.EnrollmentId, SelectedAssessment.AssessmentId, Score, token);
        SuccessMessage = "Grade saved and final result recalculated.";
        Assessments.Clear(); foreach (var item in await workspace.GetAssessmentScoresAsync(SelectedEnrollment.EnrollmentId, token)) Assessments.Add(item);
    });
}
