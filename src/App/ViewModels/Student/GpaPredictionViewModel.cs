using System.Collections.ObjectModel;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Constants;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.Student;

/// <summary>
/// GPA forecasting.
///
/// The student enters an expected score for the subjects still outstanding; the
/// screen shows the projected GPA, the classification it earns, and - separately
/// - the classification AFTER the retake penalty. Showing both is the point:
/// "8.2, but Good rather than Very Good because of 3 retaken subjects" is
/// actionable, while a single downgraded number looks like a bug.
/// </summary>
public partial class GpaPredictionViewModel : ViewModelBase, INavigationAware
{
    private readonly IGpaPredictionService _predictionService;
    private readonly IGraduationService _graduationService;
    private readonly ICurrentUserContext _currentUser;

    [ObservableProperty]
    private GpaPredictionDto? _prediction;

    [ObservableProperty]
    private ObservableCollection<PlannedSubject> _plannedSubjects = [];

    [ObservableProperty]
    private ObservableCollection<GpaPredictionDto> _history = [];

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public bool HasPrediction => Prediction is not null;

    partial void OnPredictionChanged(GpaPredictionDto? value)
    {
        OnPropertyChanged(nameof(HasPrediction));
        OnPropertyChanged(nameof(IsDemoted));
        OnPropertyChanged(nameof(DemotionMessage));
    }

    public bool IsDemoted => Prediction?.IsDemoted ?? false;

    public string DemotionMessage => Prediction?.DemotionReason ?? string.Empty;

    /// <summary>The threshold, shown so the rule is visible rather than mysterious.</summary>
    public string RetakeRuleMessage =>
        $"Từ {GraduationRules.RetakeDemotionThreshold} môn học lại trở lên, xếp loại tốt nghiệp bị giảm 1 bậc.";

    public GpaPredictionViewModel(
        IGpaPredictionService predictionService,
        IGraduationService graduationService,
        ICurrentUserContext currentUser)
    {
        _predictionService = predictionService;
        _graduationService = graduationService;
        _currentUser = currentUser;
        Title = "Dự Đoán GPA & Xếp Loại Tốt Nghiệp";
    }

    public async Task OnNavigatedToAsync(object? parameter, CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            ErrorMessage = "Màn hình này chỉ dành cho tài khoản sinh viên.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            // Seeded with the subjects still outstanding, so the student adjusts
            // scores rather than typing the whole list from scratch.
            var missing = await _graduationService.GetMissingCoursesAsync(studentId, cancellationToken);

            PlannedSubjects.Clear();
            foreach (var course in missing.Where(m => m.IsEligibleNow).Take(20))
            {
                PlannedSubjects.Add(new PlannedSubject(
                    course.CourseId, course.CourseCode, course.CourseName, course.Credits));
            }
        });

        await PredictAsync(cancellationToken);
        await LoadHistoryAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task PredictAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            StatusMessage = null;

            // Only the rows the student actually filled in. An unticked subject
            // is "no opinion", not "expect zero" - counting it as zero would
            // make every forecast look catastrophic.
            var planned = PlannedSubjects
                .Where(p => p.IsIncluded)
                .Select(p => new PlannedGradeDto(p.CourseId, p.ExpectedScore))
                .ToList();

            Prediction = await _predictionService.PredictAsync(studentId, planned, cancellationToken);
        });
    }

    [RelayCommand]
    private async Task SaveSnapshotAsync()
    {
        if (Prediction is null || _currentUser.StudentId is not int studentId)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _predictionService.SaveSnapshotAsync(studentId, Prediction);
            StatusMessage = "Đã lưu kết quả dự đoán.";
        });

        await LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.StudentId is not int studentId)
        {
            return;
        }

        var history = await _predictionService.GetHistoryAsync(studentId, 10, cancellationToken);

        History.Clear();
        foreach (var item in history)
        {
            History.Add(item);
        }
    }

    [RelayCommand]
    private void IncludeAll()
    {
        foreach (var subject in PlannedSubjects)
        {
            subject.IsIncluded = true;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var subject in PlannedSubjects)
        {
            subject.IsIncluded = false;
        }
    }

    /// <summary>One outstanding subject with the score the student expects to get.</summary>
    public partial class PlannedSubject : ObservableObject
    {
        public PlannedSubject(int courseId, string courseCode, string courseName, int credits)
        {
            CourseId = courseId;
            CourseCode = courseCode;
            CourseName = courseName;
            Credits = credits;
        }

        public int CourseId { get; }
        public string CourseCode { get; }
        public string CourseName { get; }
        public int Credits { get; }

        [ObservableProperty]
        private bool _isIncluded;

        /// <summary>
        /// Defaults to the pass mark rather than 0 or 10: it is the neutral
        /// starting point, and a default of 10 would flatter every forecast.
        /// </summary>
        [ObservableProperty]
        private decimal _expectedScore = AcademicRules.PassScore;
    }
}
