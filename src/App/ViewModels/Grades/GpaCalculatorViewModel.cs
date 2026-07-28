using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Grades;

/// <summary>Displays semester and cumulative credit-weighted GPA results.</summary>
public partial class GpaCalculatorViewModel : ViewModelBase, INavigationAware
{
    private readonly IGpaService _gpaService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty]
    private GpaSummaryDto? _summary;

    [ObservableProperty]
    private CreditSummaryDto? _credits;

    [ObservableProperty]
    private ObservableCollection<SemesterGpaDto> _semesters = [];

    public string CumulativeGpaDisplay => Summary?.CumulativeGpa?.ToString("0.00") ?? "-";
    public int GpaCredits => Summary?.BySemester.Sum(s => s.GpaCredits) ?? 0;
    public int EarnedCredits => Credits?.EarnedCredits ?? 0;
    public int FailedCredits => Credits?.FailedCredits ?? 0;
    public int InProgressCredits => Credits?.InProgressCredits ?? 0;
    public bool IsEmpty => !IsBusy && !HasError && Semesters.Count == 0;

    public GpaCalculatorViewModel(
        IGpaService gpaService,
        ICurrentUserContext currentUser)
    {
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Title = "GPA Calculator";
    }

    public Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
        => RefreshAsync(cancellationToken);

    partial void OnSummaryChanged(GpaSummaryDto? value)
    {
        OnPropertyChanged(nameof(CumulativeGpaDisplay));
        OnPropertyChanged(nameof(GpaCredits));
    }

    partial void OnCreditsChanged(CreditSummaryDto? value)
    {
        OnPropertyChanged(nameof(EarnedCredits));
        OnPropertyChanged(nameof(FailedCredits));
        OnPropertyChanged(nameof(InProgressCredits));
    }

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
            Summary = await _gpaService.GetGpaSummaryAsync(studentId, cancellationToken);
            Credits = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);

            Semesters.Clear();
            foreach (var semester in Summary.BySemester)
            {
                Semesters.Add(semester);
            }
        });

        OnPropertyChanged(nameof(IsEmpty));
    }
}
