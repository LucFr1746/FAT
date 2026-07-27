using Data;
using Domain.Constants;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Dashboard analytics and the full Statistics screen projection.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService, IStatisticsService
{
    private readonly FAT_DBContext _db;
    private readonly IGpaService _gpaService;
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserContext _currentUser;

    public AnalyticsService(
        FAT_DBContext db,
        IGpaService gpaService,
        IGradeService gradeService,
        ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<GradeDistributionDto>> GetGradeDistributionAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View grade distribution");

        var letters = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                                 && enrollment.IsCounted
                                 && (enrollment.Status == EnrollmentStatus.Passed
                                     || enrollment.Status == EnrollmentStatus.Failed)
                                 && enrollment.LetterGrade != null)
            .Select(enrollment => enrollment.LetterGrade!)
            .ToListAsync(cancellationToken);

        return letters
            .GroupBy(letter => letter)
            .OrderBy(group => group.Key)
            .Select(group => new GradeDistributionDto(
                group.Key,
                group.Count(),
                letters.Count == 0
                    ? 0m
                    : Math.Round(100m * group.Count() / letters.Count, 1)))
            .ToList();
    }

    public async Task<IReadOnlyList<GpaTrendPointDto>> GetGpaTrendAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View GPA trend");

        return (await _gpaService.GetGpaBySemesterAsync(studentId, cancellationToken))
            .Select(item => new GpaTrendPointDto(
                item.SemesterCode,
                item.DisplayOrder,
                item.Gpa,
                item.EarnedCredits))
            .ToList();
    }

    public Task<IReadOnlyList<CourseHighlightDto>> GetTopCoursesAsync(
        int studentId,
        int take = 5,
        CancellationToken cancellationToken = default)
        => GetHighlightsAsync(studentId, take, ascending: false, cancellationToken);

    public Task<IReadOnlyList<CourseHighlightDto>> GetWeakestCoursesAsync(
        int studentId,
        int take = 5,
        CancellationToken cancellationToken = default)
        => GetHighlightsAsync(studentId, take, ascending: true, cancellationToken);

    public async Task<IReadOnlyList<AcademicWarningDto>> GetAcademicWarningsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View academic warnings");

        var transcript = await _gradeService.GetTranscriptAsync(studentId, cancellationToken);

        return transcript.Semesters
            .Select(semester => new
            {
                Semester = semester,
                FailedCourses = semester.Items.Count(item =>
                    item.Status == EnrollmentStatus.Failed && item.IsCounted)
            })
            .Where(item =>
                (item.Semester.SemesterGpa.HasValue
                 && item.Semester.SemesterGpa < AcademicRules.AcademicWarningGpaThreshold)
                || item.FailedCourses >= AcademicRules.AcademicWarningFailedCourseCount)
            .Select(item => new AcademicWarningDto(
                item.Semester.SemesterCode,
                item.FailedCourses >= AcademicRules.AcademicWarningFailedCourseCount
                    ? $"Failed {item.FailedCourses} courses"
                    : "Semester GPA below the academic-warning threshold",
                item.Semester.SemesterGpa,
                item.FailedCourses))
            .ToList();
    }

    public async Task<DashboardDto> GetDashboardAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View academic dashboard");

        var student = await _db.Students
            .AsNoTracking()
            .Where(item => item.StudentId == studentId)
            .Select(item => new
            {
                item.StudentCode,
                item.FullName,
                MajorName = item.Major != null ? item.Major.MajorName : string.Empty
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Student {studentId} was not found.");

        var summary = await _gpaService.GetGpaSummaryAsync(studentId, cancellationToken);
        var credits = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);
        var transcript = await _gradeService.GetTranscriptAsync(studentId, cancellationToken);
        var allItems = transcript.Semesters.SelectMany(semester => semester.Items).ToList();
        var currentSemester = transcript.Semesters.FirstOrDefault(semester => semester.IsCurrent);

        return new DashboardDto(
            student.StudentCode,
            student.FullName,
            student.MajorName,
            summary.CumulativeGpa,
            summary.Classification,
            summary.ClassificationName,
            credits.EarnedCredits,
            credits.RequiredCredits,
            credits.InProgressCredits,
            credits.CompletionPercent,
            allItems.Count(item => item.Status == EnrollmentStatus.Passed && item.IsCounted),
            allItems.Count(item => item.Status == EnrollmentStatus.Failed && item.IsCounted),
            allItems.Count(item => item.Status == EnrollmentStatus.Studying && item.IsCounted),
            currentSemester?.SemesterCode,
            await GetGpaTrendAsync(studentId, cancellationToken),
            await GetGradeDistributionAsync(studentId, cancellationToken),
            currentSemester?.Items ?? [],
            await GetAcademicWarningsAsync(studentId, cancellationToken));
    }

    public async Task<AcademicStatisticsDto> GetStatisticsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View academic statistics");

        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                                 && enrollment.IsCounted
                                 && enrollment.Status != EnrollmentStatus.Withdrawn)
            .Select(enrollment => new
            {
                enrollment.Status,
                enrollment.FinalScore,
                enrollment.LetterGrade,
                CourseCode = enrollment.Course != null ? enrollment.Course.CourseCode : string.Empty,
                CourseName = enrollment.Course != null ? enrollment.Course.CourseName : string.Empty,
                Credits = enrollment.Course != null ? enrollment.Course.Credits : 0,
                CountsTowardGpa = enrollment.Course != null && enrollment.Course.CountsTowardGpa,
                GradeCount = enrollment.Grades.Count
            })
            .ToListAsync(cancellationToken);

        var gpaSummary = await _gpaService.GetGpaSummaryAsync(studentId, cancellationToken);
        var creditSummary = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);
        var gpaTrend = gpaSummary.BySemester
            .Select(item => new GpaTrendPointDto(
                item.SemesterCode,
                item.DisplayOrder,
                item.Gpa,
                item.EarnedCredits))
            .ToList();

        var completedRows = rows
            .Where(row => row.Status is EnrollmentStatus.Passed or EnrollmentStatus.Failed
                          && row.FinalScore.HasValue)
            .ToList();

        var highest = completedRows
            .OrderByDescending(row => row.FinalScore)
            .ThenBy(row => row.CourseCode)
            .FirstOrDefault();

        var lowest = completedRows
            .OrderBy(row => row.FinalScore)
            .ThenBy(row => row.CourseCode)
            .FirstOrDefault();

        var passedCourses = rows.Count(row => row.Status == EnrollmentStatus.Passed);
        var failedCourses = rows.Count(row => row.Status == EnrollmentStatus.Failed);
        var studyingCourses = rows.Count(row => row.Status == EnrollmentStatus.Studying);
        var notGradedCourses = rows.Count(row =>
            row.Status == EnrollmentStatus.Studying && row.GradeCount == 0);
        var totalCredits = rows.Sum(row => row.Credits);
        var completedCredits = rows
            .Where(row => row.Status == EnrollmentStatus.Passed)
            .Sum(row => row.Credits);
        var failedCredits = rows
            .Where(row => row.Status == EnrollmentStatus.Failed)
            .Sum(row => row.Credits);
        var gpaCredits = rows
            .Where(row => row.Status == EnrollmentStatus.Passed && row.CountsTowardGpa)
            .Sum(row => row.Credits);
        var progress = creditSummary.RequiredCredits <= 0
            ? 0m
            : Math.Round(
                100m * creditSummary.EarnedCredits / creditSummary.RequiredCredits,
                1);

        var statusCounts = new[]
        {
            new { Status = "Passed", Count = passedCourses },
            new { Status = "Failed", Count = failedCourses },
            new
            {
                Status = "Studying",
                Count = studyingCourses - notGradedCourses
            },
            new { Status = "Not Graded", Count = notGradedCourses }
        };

        var statusDistribution = statusCounts
            .Select(item => new StatusDistributionDto(
                item.Status,
                item.Count,
                rows.Count == 0 ? 0m : Math.Round(100m * item.Count / rows.Count, 1)))
            .ToList();

        return new AcademicStatisticsDto(
            gpaSummary.CumulativeGpa,
            gpaTrend,
            rows.Count,
            passedCourses,
            failedCourses,
            studyingCourses,
            notGradedCourses,
            totalCredits,
            gpaCredits,
            completedCredits,
            failedCredits,
            Math.Max(0, totalCredits - completedCredits),
            creditSummary.RequiredCredits,
            progress,
            completedRows.Count == 0
                ? null
                : AcademicRules.RoundFinalScore(completedRows.Average(row => row.FinalScore!.Value)),
            highest is null
                ? null
                : new CourseHighlightDto(
                    highest.CourseCode,
                    highest.CourseName,
                    highest.FinalScore!.Value,
                    highest.LetterGrade),
            lowest is null
                ? null
                : new CourseHighlightDto(
                    lowest.CourseCode,
                    lowest.CourseName,
                    lowest.FinalScore!.Value,
                    lowest.LetterGrade),
            statusDistribution);
    }

    private async Task<IReadOnlyList<CourseHighlightDto>> GetHighlightsAsync(
        int studentId,
        int take,
        bool ascending,
        CancellationToken cancellationToken)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View course highlights");

        if (take < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "The result count cannot be negative.");
        }

        var query = _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                                 && enrollment.IsCounted
                                 && enrollment.FinalScore != null);

        query = ascending
            ? query.OrderBy(enrollment => enrollment.FinalScore)
            : query.OrderByDescending(enrollment => enrollment.FinalScore);

        return await query
            .Take(take)
            .Select(enrollment => new CourseHighlightDto(
                enrollment.Course != null ? enrollment.Course.CourseCode : string.Empty,
                enrollment.Course != null ? enrollment.Course.CourseName : string.Empty,
                enrollment.FinalScore!.Value,
                enrollment.LetterGrade))
            .ToListAsync(cancellationToken);
    }
}
