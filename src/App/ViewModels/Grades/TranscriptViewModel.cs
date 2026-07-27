using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

/// <summary>Chronological transcript grouped by calendar semester.</summary>
public partial class TranscriptViewModel : ViewModelBase, INavigationAware
{
    private readonly IGradeService _gradeService;
    private readonly IGpaService _gpaService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty]
    private TranscriptDto? _transcript;

    [ObservableProperty]
    private GpaSummaryDto? _gpaSummary;

    [ObservableProperty]
    private CreditSummaryDto? _creditSummary;

    [ObservableProperty]
    private ObservableCollection<SemesterTranscriptDto> _semesters = [];

    public string CumulativeGpaDisplay => GpaSummary?.CumulativeGpa?.ToString("0.00") ?? "-";
    public int EarnedCredits => CreditSummary?.EarnedCredits ?? 0;
    public bool IsEmpty => !IsBusy && !HasError && Semesters.Count == 0;

    public TranscriptViewModel(
        IGradeService gradeService,
        IGpaService gpaService,
        ICurrentUserContext currentUser)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "Transcript";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnGpaSummaryChanged(GpaSummaryDto? value)
        => OnPropertyChanged(nameof(CumulativeGpaDisplay));

    partial void OnCreditSummaryChanged(CreditSummaryDto? value)
        => OnPropertyChanged(nameof(EarnedCredits));

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
            Transcript = await _gradeService.GetTranscriptAsync(studentId, cancellationToken);
            GpaSummary = await _gpaService.GetGpaSummaryAsync(studentId, cancellationToken);
            CreditSummary = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);

            Semesters.Clear();
            foreach (var semester in Transcript.Semesters)
            {
                Semesters.Add(semester);
            }
        });

        OnPropertyChanged(nameof(IsEmpty));
    }
}
