using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.GradeGpa;

public sealed partial class StatisticsViewModel : StudentAcademicViewModelBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsViewModel(
        IStatisticsService statisticsService,
        ICurrentUserContext currentUser)
        : base(currentUser)
    {
        _statisticsService = statisticsService
            ?? throw new ArgumentNullException(nameof(statisticsService));
        Title = "Statistics";
    }

    [ObservableProperty]
    private AcademicStatisticsDto? _statistics;

    [ObservableProperty]
    private ISeries[] _gpaSeries = [];

    [ObservableProperty]
    private Axis[] _gpaXAxes = [];

    [ObservableProperty]
    private ISeries[] _statusSeries = [];

    [ObservableProperty]
    private Axis[] _statusXAxes = [];

    public bool IsEmpty => Statistics?.TotalCourses is null or 0;

    protected override Task LoadAsync(CancellationToken cancellationToken)
        => RefreshCoreAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
        => RefreshCoreAsync(CancellationToken.None);

    private Task RefreshCoreAsync(CancellationToken cancellationToken)
        => RunBusyAsync(async () =>
        {
            Statistics = await _statisticsService.GetStatisticsAsync(
                StudentId,
                cancellationToken);

            var gpaPoints = Statistics.GpaBySemester
                .Where(point => point.Gpa.HasValue)
                .ToList();

            GpaSeries = gpaPoints.Count == 0
                ? []
                :
                [
                    new LineSeries<double>
                    {
                        Name = "Semester GPA",
                        Values = gpaPoints.Select(point => (double)point.Gpa!.Value).ToArray(),
                        GeometrySize = 10
                    }
                ];

            GpaXAxes =
            [
                new Axis
                {
                    Labels = gpaPoints.Select(point => point.SemesterCode).ToArray()
                }
            ];

            StatusSeries =
            [
                new ColumnSeries<int>
                {
                    Name = "Courses",
                    Values = Statistics.StatusDistribution
                        .Select(item => item.Count)
                        .ToArray()
                }
            ];

            StatusXAxes =
            [
                new Axis
                {
                    Labels = Statistics.StatusDistribution
                        .Select(item => item.Status)
                        .ToArray()
                }
            ];

            OnPropertyChanged(nameof(IsEmpty));
        });
}
