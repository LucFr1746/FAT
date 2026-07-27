using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

public sealed record StatusDistributionItem(string Status, int Count, decimal Percent);

/// <summary>Academic KPIs and chart-ready series for the signed-in student.</summary>
public partial class StatisticsViewModel : ViewModelBase, INavigationAware
{
    private readonly IAnalyticsService _analytics;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty]
    private DashboardDto? _dashboard;

    [ObservableProperty]
    private CourseHighlightDto? _highestCourse;

    [ObservableProperty]
    private CourseHighlightDto? _lowestCourse;

    [ObservableProperty]
    private ObservableCollection<GradeDistributionDto> _gradeDistribution = [];

    [ObservableProperty]
    private ObservableCollection<StatusDistributionItem> _statusDistribution = [];

    public ObservableCollection<ISeries> GpaSeries { get; } = [];
    public ObservableCollection<ISeries> StatusSeries { get; } = [];
    public ObservableCollection<Axis> GpaXAxes { get; } = [new Axis()];
    public ObservableCollection<Axis> GpaYAxes { get; } =
    [
        new Axis { MinLimit = 0, MaxLimit = 10 }
    ];

    public bool IsEmpty => !IsBusy && !HasError && Dashboard?.TotalCourses == 0;
    public string GpaDisplay => Dashboard?.CumulativeGpa?.ToString("0.00") ?? "-";
    public string AverageScoreDisplay => Dashboard?.AverageFinalScore?.ToString("0.0") ?? "-";
    public string HighestCourseDisplay => HighestCourse is null
        ? "-"
        : $"{HighestCourse.CourseCode} ({HighestCourse.FinalScore:0.0})";
    public string LowestCourseDisplay => LowestCourse is null
        ? "-"
        : $"{LowestCourse.CourseCode} ({LowestCourse.FinalScore:0.0})";

    public StatisticsViewModel(
        IAnalyticsService analytics,
        ICurrentUserContext currentUser)
    {
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "Statistics";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnDashboardChanged(DashboardDto? value)
    {
        OnPropertyChanged(nameof(GpaDisplay));
        OnPropertyChanged(nameof(AverageScoreDisplay));
    }

    partial void OnHighestCourseChanged(CourseHighlightDto? value)
        => OnPropertyChanged(nameof(HighestCourseDisplay));

    partial void OnLowestCourseChanged(CourseHighlightDto? value)
        => OnPropertyChanged(nameof(LowestCourseDisplay));

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            Dashboard = await _analytics.GetDashboardAsync(studentId, cancellationToken);
            var top = await _analytics.GetTopCoursesAsync(studentId, 1, cancellationToken);
            var weakest = await _analytics.GetWeakestCoursesAsync(studentId, 1, cancellationToken);

            HighestCourse = top.FirstOrDefault();
            LowestCourse = weakest.FirstOrDefault();

            GradeDistribution.Clear();
            foreach (var item in Dashboard.GradeDistribution)
            {
                GradeDistribution.Add(item);
            }

            BuildStatusDistribution(Dashboard);
            BuildCharts(Dashboard);
        });

        OnPropertyChanged(nameof(IsEmpty));
    }

    private void BuildStatusDistribution(DashboardDto dashboard)
    {
        var total = dashboard.PassedCourses + dashboard.FailedCourses + dashboard.StudyingCourses;
        StatusDistribution.Clear();

        AddStatus("Passed", dashboard.PassedCourses);
        AddStatus("Failed", dashboard.FailedCourses);
        AddStatus("Studying", dashboard.StudyingCourses);

        void AddStatus(string status, int count)
        {
            var percent = total == 0 ? 0m : Math.Round(100m * count / total, 1);
            StatusDistribution.Add(new StatusDistributionItem(status, count, percent));
        }
    }

    private void BuildCharts(DashboardDto dashboard)
    {
        var trend = dashboard.GpaTrend.OrderBy(t => t.DisplayOrder).ToList();

        GpaSeries.Clear();
        GpaSeries.Add(new LineSeries<double?>
        {
            Name = "GPA",
            Values = trend.Select(t => t.Gpa.HasValue ? (double?)t.Gpa.Value : null).ToArray(),
            GeometrySize = 10
        });

        GpaXAxes[0].Labels = trend.Select(t => t.SemesterCode).ToArray();

        StatusSeries.Clear();
        foreach (var status in StatusDistribution.Where(s => s.Count > 0))
        {
            StatusSeries.Add(new PieSeries<int>
            {
                Name = status.Status,
                Values = [status.Count]
            });
        }
    }
}
