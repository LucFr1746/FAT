using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.GradeGpa;

public sealed partial class GpaCalculatorViewModel : StudentAcademicViewModelBase
{
    private readonly IGpaService _gpaService;
    private readonly IStatisticsService _statisticsService;

    public GpaCalculatorViewModel(
        IGpaService gpaService,
        IStatisticsService statisticsService,
        ICurrentUserContext currentUser)
        : base(currentUser)
    {
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _statisticsService = statisticsService
            ?? throw new ArgumentNullException(nameof(statisticsService));
        Title = "GPA Calculator";
    }

    [ObservableProperty]
    private GpaSummaryDto? _summary;

    [ObservableProperty]
    private CreditSummaryDto? _credits;

    [ObservableProperty]
    private AcademicStatisticsDto? _statistics;

    public bool IsEmpty => Summary?.BySemester.Count is null or 0;

    protected override Task LoadAsync(CancellationToken cancellationToken)
        => RefreshCoreAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
        => RefreshCoreAsync(CancellationToken.None);

    private Task RefreshCoreAsync(CancellationToken cancellationToken)
        => RunBusyAsync(async () =>
        {
            Summary = await _gpaService.GetGpaSummaryAsync(StudentId, cancellationToken);
            Credits = await _gpaService.GetCreditSummaryAsync(StudentId, cancellationToken);
            Statistics = await _statisticsService.GetStatisticsAsync(
                StudentId,
                cancellationToken);
            OnPropertyChanged(nameof(IsEmpty));
        });
}
