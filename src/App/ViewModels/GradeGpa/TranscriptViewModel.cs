using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.GradeGpa;

public sealed partial class TranscriptViewModel : StudentAcademicViewModelBase
{
    private readonly IGradeService _gradeService;
    private readonly IGpaService _gpaService;

    public TranscriptViewModel(
        IGradeService gradeService,
        IGpaService gpaService,
        ICurrentUserContext currentUser)
        : base(currentUser)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        Title = "Transcript";
    }

    [ObservableProperty]
    private TranscriptDto? _transcript;

    [ObservableProperty]
    private decimal? _cumulativeGpa;

    [ObservableProperty]
    private int _completedCredits;

    public bool IsEmpty => Transcript?.Semesters.Count is null or 0;

    protected override Task LoadAsync(CancellationToken cancellationToken)
        => RefreshCoreAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
        => RefreshCoreAsync(CancellationToken.None);

    private Task RefreshCoreAsync(CancellationToken cancellationToken)
        => RunBusyAsync(async () =>
        {
            Transcript = await _gradeService.GetTranscriptAsync(StudentId, cancellationToken);
            CumulativeGpa = await _gpaService.GetCumulativeGpaAsync(
                StudentId,
                cancellationToken);
            CompletedCredits = (await _gpaService.GetCreditSummaryAsync(
                StudentId,
                cancellationToken)).EarnedCredits;
            OnPropertyChanged(nameof(IsEmpty));
        });
}
