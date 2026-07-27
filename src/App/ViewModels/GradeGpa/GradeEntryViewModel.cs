using System.Collections.ObjectModel;
using System.Globalization;
using App.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Services.Abstractions;
using Services.Dtos;

namespace App.ViewModels.GradeGpa;

public sealed partial class GradeEntryViewModel : ViewModelBase, INavigationAware
{
    private readonly IGradeService _gradeService;
    private readonly IGradeWorkspaceService _workspace;
    private int _studentSelectionVersion;
    private int _enrollmentSelectionVersion;

    public GradeEntryViewModel(
        IGradeService gradeService,
        IGradeWorkspaceService workspace)
    {
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Title = "Manage Grades";
    }

    public ObservableCollection<GradeStudentOptionDto> Students { get; } = [];
    public ObservableCollection<GradeEnrollmentOptionDto> Enrollments { get; } = [];
    public ObservableCollection<GradeAssessmentDto> Assessments { get; } = [];

    [ObservableProperty]
    private GradeStudentOptionDto? _selectedStudent;

    [ObservableProperty]
    private GradeEnrollmentOptionDto? _selectedEnrollment;

    [ObservableProperty]
    private GradeAssessmentDto? _selectedAssessment;

    [ObservableProperty]
    private string _scoreText = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isDeleteConfirmationVisible;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool HasSuccessMessage => !string.IsNullOrWhiteSpace(SuccessMessage);
    public bool CanDelete => SelectedAssessment?.HasScore == true;
    public string EditModeLabel => SelectedAssessment?.HasScore == true
        ? "Edit recorded grade"
        : "Add grade";

    public Task OnNavigatedToAsync(
        object? parameter,
        CancellationToken cancellationToken = default)
        => RefreshCoreAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
        => RefreshCoreAsync(CancellationToken.None);

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryValidateSelectionAndScore(out var score))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var enrollmentId = SelectedEnrollment!.EnrollmentId;
            var assessmentId = SelectedAssessment!.AssessmentId;

            await _gradeService.UpsertGradeAsync(
                enrollmentId,
                assessmentId,
                score,
                cancellationToken);

            await ReloadCurrentSelectionAsync(enrollmentId, assessmentId, cancellationToken);
            SuccessMessage = "Grade saved and the final result was recalculated.";
            ValidationMessage = null;
        });
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ScoreText = SelectedAssessment?.Score?.ToString(
            "0.##",
            CultureInfo.CurrentCulture) ?? string.Empty;
        ValidationMessage = null;
        SuccessMessage = null;
        IsDeleteConfirmationVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void RequestDelete()
    {
        SuccessMessage = null;
        ValidationMessage = null;
        IsDeleteConfirmationVisible = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteConfirmationVisible = false;

    [RelayCommand]
    private async Task ConfirmDeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedEnrollment is null || SelectedAssessment?.HasScore != true)
        {
            IsDeleteConfirmationVisible = false;
            ValidationMessage = "Select an assessment that already has a recorded grade.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var enrollmentId = SelectedEnrollment.EnrollmentId;
            var assessmentId = SelectedAssessment.AssessmentId;

            await _workspace.DeleteGradeAsync(
                enrollmentId,
                assessmentId,
                cancellationToken);

            IsDeleteConfirmationVisible = false;
            await ReloadCurrentSelectionAsync(enrollmentId, assessmentId, cancellationToken);
            SuccessMessage = "Grade deleted and the course result was recalculated.";
            ValidationMessage = null;
        });
    }

    partial void OnSelectedStudentChanged(GradeStudentOptionDto? value)
    {
        _studentSelectionVersion++;
        var version = _studentSelectionVersion;

        SelectedEnrollment = null;
        SelectedAssessment = null;
        Enrollments.Clear();
        Assessments.Clear();
        ClearEditorMessages();

        if (value is not null)
        {
            _ = LoadEnrollmentsAsync(value.StudentId, version);
        }
    }

    partial void OnSelectedEnrollmentChanged(GradeEnrollmentOptionDto? value)
    {
        _enrollmentSelectionVersion++;
        var version = _enrollmentSelectionVersion;

        SelectedAssessment = null;
        Assessments.Clear();
        ClearEditorMessages();

        if (value is not null)
        {
            _ = LoadAssessmentsAsync(value.EnrollmentId, version);
        }
    }

    partial void OnSelectedAssessmentChanged(GradeAssessmentDto? value)
    {
        ScoreText = value?.Score?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        ValidationMessage = null;
        SuccessMessage = null;
        IsDeleteConfirmationVisible = false;
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(EditModeLabel));
        RequestDeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnScoreTextChanged(string value)
    {
        SuccessMessage = null;
        ValidateScoreText(showRequiredError: false, out _);
    }

    partial void OnValidationMessageChanged(string? value)
        => OnPropertyChanged(nameof(HasValidationError));

    partial void OnSuccessMessageChanged(string? value)
        => OnPropertyChanged(nameof(HasSuccessMessage));

    private Task RefreshCoreAsync(CancellationToken cancellationToken)
        => RunBusyAsync(async () =>
        {
            var students = await _workspace.GetStudentsAsync(cancellationToken);

            _studentSelectionVersion++;
            _enrollmentSelectionVersion++;
            SelectedStudent = null;
            Students.Clear();
            Enrollments.Clear();
            Assessments.Clear();

            foreach (var student in students)
            {
                Students.Add(student);
            }

            ClearEditorMessages();
        });

    private Task LoadEnrollmentsAsync(int studentId, int version)
        => RunBusyAsync(async () =>
        {
            var enrollments = await _workspace.GetEnrollmentsAsync(studentId);
            if (version != _studentSelectionVersion)
            {
                return;
            }

            Enrollments.Clear();
            foreach (var enrollment in enrollments)
            {
                Enrollments.Add(enrollment);
            }
        });

    private Task LoadAssessmentsAsync(int enrollmentId, int version)
        => RunBusyAsync(async () =>
        {
            var assessments = await _workspace.GetAssessmentScoresAsync(enrollmentId);
            if (version != _enrollmentSelectionVersion)
            {
                return;
            }

            Assessments.Clear();
            foreach (var assessment in assessments)
            {
                Assessments.Add(assessment);
            }
        });

    private async Task ReloadCurrentSelectionAsync(
        int enrollmentId,
        int assessmentId,
        CancellationToken cancellationToken)
    {
        var studentId = SelectedStudent!.StudentId;
        var enrollments = await _workspace.GetEnrollmentsAsync(studentId, cancellationToken);
        var assessments = await _workspace.GetAssessmentScoresAsync(enrollmentId, cancellationToken);

        _studentSelectionVersion++;
        _enrollmentSelectionVersion++;

        Enrollments.Clear();
        foreach (var enrollment in enrollments)
        {
            Enrollments.Add(enrollment);
        }

        SelectedEnrollment = Enrollments.FirstOrDefault(item => item.EnrollmentId == enrollmentId);

        Assessments.Clear();
        foreach (var assessment in assessments)
        {
            Assessments.Add(assessment);
        }

        SelectedAssessment = Assessments.FirstOrDefault(
            item => item.AssessmentId == assessmentId);
    }

    private bool TryValidateSelectionAndScore(out decimal score)
    {
        score = 0m;

        if (SelectedStudent is null)
        {
            ValidationMessage = "Student is required.";
            return false;
        }

        if (SelectedEnrollment is null)
        {
            ValidationMessage = "Course enrollment is required.";
            return false;
        }

        if (SelectedAssessment is null)
        {
            ValidationMessage = "Assessment is required.";
            return false;
        }

        return ValidateScoreText(showRequiredError: true, out score);
    }

    private bool ValidateScoreText(bool showRequiredError, out decimal score)
    {
        score = 0m;

        if (string.IsNullOrWhiteSpace(ScoreText))
        {
            ValidationMessage = showRequiredError ? "Score is required." : null;
            return false;
        }

        var valid = decimal.TryParse(
            ScoreText,
            NumberStyles.Number,
            CultureInfo.CurrentCulture,
            out score);

        if (!valid && CultureInfo.CurrentCulture != CultureInfo.InvariantCulture)
        {
            valid = decimal.TryParse(
                ScoreText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out score);
        }

        if (!valid)
        {
            ValidationMessage = "Score must be a valid number.";
            return false;
        }

        if (score < 0m)
        {
            ValidationMessage = "Score cannot be less than 0.";
            return false;
        }

        if (score > GradeAssessmentDto.MaximumScore)
        {
            ValidationMessage =
                $"Score cannot be greater than {GradeAssessmentDto.MaximumScore:N0}.";
            return false;
        }

        ValidationMessage = null;
        return true;
    }

    private void ClearEditorMessages()
    {
        ValidationMessage = null;
        SuccessMessage = null;
        IsDeleteConfirmationVisible = false;
        ScoreText = string.Empty;
    }
}
