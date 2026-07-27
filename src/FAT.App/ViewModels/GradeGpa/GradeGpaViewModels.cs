using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FAT.App.Navigation;
using FAT.Domain.Entities;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;

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
    protected override Task LoadAsync(object? parameter, CancellationToken token) => RunBusyAsync(async () =>
    { Dashboard = await service.GetDashboardAsync(StudentId, token); TopCourses = await service.GetTopCoursesAsync(StudentId, cancellationToken: token); WeakestCourses = await service.GetWeakestCoursesAsync(StudentId, cancellationToken: token); });
}

public sealed partial class GradeEntryViewModel(IGradeService service) : ViewModelBase, INavigationAware
{
    [ObservableProperty] private int _enrollmentId;
    [ObservableProperty] private Grade? _selectedGrade;
    [ObservableProperty] private decimal _score;
    public ObservableCollection<Grade> Grades { get; } = [];
    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    { if (parameter is int id) EnrollmentId = id; return LoadAsync(cancellationToken); }
    [RelayCommand] private Task LoadAsync(CancellationToken token = default) => RunBusyAsync(async () =>
    { Grades.Clear(); foreach (var grade in await service.GetGradesAsync(EnrollmentId, token)) Grades.Add(grade); });
    partial void OnSelectedGradeChanged(Grade? value) { if (value is not null) Score = value.Score; }
    [RelayCommand] private Task SaveAsync(CancellationToken token = default) => RunBusyAsync(async () =>
    { if (SelectedGrade is null) throw new InvalidOperationException("Select an assessment first."); await service.UpsertGradeAsync(EnrollmentId, SelectedGrade.AssessmentId, Score, token); await LoadCoreAsync(token); });
    private async Task LoadCoreAsync(CancellationToken token) { Grades.Clear(); foreach (var grade in await service.GetGradesAsync(EnrollmentId, token)) Grades.Add(grade); }
}
