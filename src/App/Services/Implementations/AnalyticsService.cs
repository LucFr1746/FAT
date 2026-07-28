using Data;
using Domain.Constants;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Read-only academic statistics calculated from existing student enrollments.
/// Nothing produced here is persisted.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IGpaService _gpaService;

    public AnalyticsService(
        FAT_DBContext db,
        ICurrentUserContext currentUser,
        IGpaService gpaService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
    }

    public async Task<DashboardDto> GetDashboardAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem thống kê");

        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                s.StudentCode,
                s.FullName,
                MajorName = s.Major != null ? s.Major.MajorName : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var attempts = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.IsCounted)
            .Select(e => new
            {
                e.EnrollmentId,
                e.Status,
                e.FinalScore,
                e.LetterGrade,
                e.GradePoint,
                e.AttemptNo,
                e.CourseId,
                e.Course!.CourseCode,
                e.Course.CourseName,
                e.Course.Credits,
                e.SemesterId,
                e.Semester!.SemesterCode,
                e.Semester.IsCurrent,
                RequiresComponentGrades = e.Student!.CurrentTermNo != null,
                HasCompleteComponentGrades =
                    e.Course.Assessments.Any()
                    && e.Course.Assessments.All(a =>
                        e.Grades.Any(g => g.AssessmentId == a.AssessmentId))
            })
            .ToListAsync(cancellationToken);

        var gpaSummary = await _gpaService.GetGpaSummaryAsync(studentId, cancellationToken);
        var creditSummary = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);
        var trend = await GetGpaTrendAsync(studentId, cancellationToken);
        var distribution = await GetGradeDistributionAsync(studentId, cancellationToken);
        var warnings = await GetAcademicWarningsAsync(studentId, cancellationToken);

        var officialAttempts = attempts
            .Where(a => a.Status != EnrollmentStatus.Withdrawn
                        && (a.Status == EnrollmentStatus.Studying
                            || !a.RequiresComponentGrades
                            || a.HasCompleteComponentGrades))
            .ToList();

        var currentCourses = attempts
            .Where(a => (a.IsCurrent || a.Status == EnrollmentStatus.Studying)
                        && (a.Status == EnrollmentStatus.Studying
                            || !a.RequiresComponentGrades
                            || a.HasCompleteComponentGrades))
            .OrderBy(a => a.CourseCode)
            .Select(a => new TranscriptItemDto(
                a.EnrollmentId,
                a.CourseCode,
                a.CourseName,
                a.Credits,
                a.FinalScore,
                a.LetterGrade,
                a.GradePoint,
                a.Status,
                IsCounted: true,
                a.AttemptNo))
            .ToList();

        var averageFinalScore = officialAttempts.Any(a => a.FinalScore.HasValue)
            ? AcademicRules.RoundFinalScore(
                officialAttempts.Where(a => a.FinalScore.HasValue).Average(a => a.FinalScore!.Value))
            : (decimal?)null;

        return new DashboardDto(
            student.StudentCode,
            student.FullName,
            student.MajorName,
            gpaSummary.CumulativeGpa,
            gpaSummary.Classification,
            gpaSummary.ClassificationName,
            creditSummary.EarnedCredits,
            creditSummary.RequiredCredits,
            creditSummary.InProgressCredits,
            creditSummary.CompletionPercent,
            officialAttempts.Count(a => a.Status == EnrollmentStatus.Passed),
            officialAttempts.Count(a => a.Status == EnrollmentStatus.Failed),
            officialAttempts.Count(a => a.Status == EnrollmentStatus.Studying),
            attempts.FirstOrDefault(a => a.IsCurrent)?.SemesterCode,
            trend,
            distribution,
            currentCourses,
            warnings,
            averageFinalScore,
            officialAttempts.Select(a => a.CourseId).Distinct().Count(),
            creditSummary.RemainingCredits);
    }

    public async Task<IReadOnlyList<GradeDistributionDto>> GetGradeDistributionAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem phân bố điểm");

        var letters = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                        && e.IsCounted
                        && e.Status != EnrollmentStatus.Withdrawn
                        && e.FinalScore != null
                        && (e.Student!.CurrentTermNo == null
                            || (e.Course!.Assessments.Any()
                                && e.Course.Assessments.All(a =>
                                    e.Grades.Any(g => g.AssessmentId == a.AssessmentId)))))
            .Select(e => e.LetterGrade ?? "N/A")
            .ToListAsync(cancellationToken);

        if (letters.Count == 0)
        {
            return [];
        }

        return letters
            .GroupBy(letter => letter)
            .Select(g => new GradeDistributionDto(
                g.Key,
                g.Count(),
                Math.Round(100m * g.Count() / letters.Count, 1)))
            .OrderByDescending(d => d.Count)
            .ThenBy(d => d.LetterGrade)
            .ToList();
    }

    public async Task<IReadOnlyList<GpaTrendPointDto>> GetGpaTrendAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem xu hướng GPA");

        var rows = await _gpaService.GetGpaBySemesterAsync(studentId, cancellationToken);

        return rows
            .Select(r => new GpaTrendPointDto(
                r.SemesterCode,
                r.DisplayOrder,
                r.Gpa,
                r.GpaCredits))
            .ToList();
    }

    public Task<IReadOnlyList<CourseHighlightDto>> GetTopCoursesAsync(
        int studentId,
        int take = 5,
        CancellationToken cancellationToken = default)
        => GetCourseHighlightsAsync(
            studentId, take, descending: true, "Xem môn có điểm cao nhất", cancellationToken);

    public Task<IReadOnlyList<CourseHighlightDto>> GetWeakestCoursesAsync(
        int studentId,
        int take = 5,
        CancellationToken cancellationToken = default)
        => GetCourseHighlightsAsync(
            studentId, take, descending: false, "Xem môn có điểm thấp nhất", cancellationToken);

    public async Task<IReadOnlyList<AcademicWarningDto>> GetAcademicWarningsAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem cảnh báo học tập");

        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                        && e.IsCounted
                        && e.Status != EnrollmentStatus.Withdrawn
                        && (e.Status == EnrollmentStatus.Studying
                            || e.Student!.CurrentTermNo == null
                            || (e.Course!.Assessments.Any()
                                && e.Course.Assessments.All(a =>
                                    e.Grades.Any(g => g.AssessmentId == a.AssessmentId)))))
            .Select(e => new
            {
                e.SemesterId,
                e.Semester!.SemesterCode,
                e.Semester.DisplayOrder,
                e.Status,
                e.FinalScore,
                e.Course!.Credits,
                e.Course.CountsTowardGpa
            })
            .ToListAsync(cancellationToken);

        var warnings = new List<AcademicWarningDto>();

        foreach (var semester in rows
                     .GroupBy(r => new { r.SemesterId, r.SemesterCode, r.DisplayOrder })
                     .OrderBy(g => g.Key.DisplayOrder))
        {
            var failedCourses = semester.Count(r => r.Status == EnrollmentStatus.Failed);
            var completedForAverage = semester
                .Where(r => r.FinalScore.HasValue && r.CountsTowardGpa)
                .Select(r => (r.FinalScore!.Value, r.Credits));
            var semesterAverage = GpaService.CalculateGpa(completedForAverage);

            if (semesterAverage is null
                || (semesterAverage >= AcademicRules.AcademicWarningGpaThreshold
                    && failedCourses < AcademicRules.AcademicWarningFailedCourseCount))
            {
                continue;
            }

            var reasons = new List<string>();
            if (semesterAverage < AcademicRules.AcademicWarningGpaThreshold)
            {
                reasons.Add($"điểm trung bình dưới {AcademicRules.AcademicWarningGpaThreshold:0.0}");
            }

            if (failedCourses >= AcademicRules.AcademicWarningFailedCourseCount)
            {
                reasons.Add($"có {failedCourses} môn Failed");
            }

            warnings.Add(new AcademicWarningDto(
                semester.Key.SemesterCode,
                string.Join(" và ", reasons),
                semesterAverage,
                failedCourses));
        }

        return warnings;
    }

    private async Task<IReadOnlyList<CourseHighlightDto>> GetCourseHighlightsAsync(
        int studentId,
        int take,
        bool descending,
        string operation,
        CancellationToken cancellationToken)
    {
        _currentUser.RequireSelfOrAdmin(studentId, operation);

        var query = _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                        && e.IsCounted
                        && e.Status != EnrollmentStatus.Withdrawn
                        && e.FinalScore != null
                        && (e.Student!.CurrentTermNo == null
                            || (e.Course!.Assessments.Any()
                                && e.Course.Assessments.All(a =>
                                    e.Grades.Any(g => g.AssessmentId == a.AssessmentId)))));

        query = descending
            ? query.OrderByDescending(e => e.FinalScore).ThenBy(e => e.Course!.CourseCode)
            : query.OrderBy(e => e.FinalScore).ThenBy(e => e.Course!.CourseCode);

        return await query
            .Take(Math.Clamp(take, 0, 100))
            .Select(e => new CourseHighlightDto(
                e.Course!.CourseCode,
                e.Course.CourseName,
                e.FinalScore!.Value,
                e.LetterGrade))
            .ToListAsync(cancellationToken);
    }
}
