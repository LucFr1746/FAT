using Data;
using Domain.Constants;
using Domain.Enums;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// Curriculum progress: the study path measured against what has been passed.
/// </summary>
public sealed class GraduationService : IGraduationService
{
    private readonly FAT_DBContext _db;
    private readonly IGpaService _gpaService;
    private readonly IPrerequisiteService _prerequisiteService;

    public GraduationService(
        FAT_DBContext db,
        IGpaService gpaService,
        IPrerequisiteService prerequisiteService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _prerequisiteService = prerequisiteService ?? throw new ArgumentNullException(nameof(prerequisiteService));
    }

    public async Task<GraduationProgressDto> GetProgressAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var credits = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);
        var gpa = await _gpaService.GetCumulativeGpaAsync(studentId, cancellationToken);
        var missing = await GetMissingCoursesAsync(studentId, cancellationToken);

        var totalCurriculumCourses = await _db.CurriculumItems
            .CountAsync(ci => ci.Major!.Students.Any(s => s.StudentId == studentId), cancellationToken);

        var completedCourses = totalCurriculumCourses - missing.Count;

        // Retaken subjects can pull the classification down, so the projection a
        // student sees here matches the one on the prediction screen.
        var retakenSubjects = await CountRetakenSubjectsAsync(studentId, cancellationToken);
        var classification = GraduationRules.ClassifyWithRetakes(gpa ?? 0m, retakenSubjects);

        return new GraduationProgressDto(
            EarnedCredits: credits.EarnedCredits,
            RequiredCredits: credits.RequiredCredits,
            TotalCurriculumCourses: totalCurriculumCourses,
            CompletedCourses: completedCourses,
            ProgressPercent: credits.CompletionPercent,
            CurrentGpa: gpa,
            ProjectedClassification: classification,
            ProjectedClassificationName: AcademicRules.GetClassificationName(classification),
            MissingCourses: missing);
    }

    public async Task<IReadOnlyList<MissingCourseDto>> GetMissingCoursesAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var majorId = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => (int?)s.MajorId)
            .FirstOrDefaultAsync(cancellationToken);

        if (majorId is null)
        {
            return [];
        }

        var passedCourseIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Passed)
            .Select(e => e.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var outstanding = await _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.MajorId == majorId && !passedCourseIds.Contains(ci.CourseId))
            .OrderBy(ci => ci.TermNo)
            .ThenBy(ci => ci.DisplayOrder)
            .Select(ci => new
            {
                ci.CourseId,
                ci.Course!.CourseCode,
                ci.Course.CourseName,
                ci.Course.Credits,
                ci.TermNo
            })
            .ToListAsync(cancellationToken);

        if (outstanding.Count == 0)
        {
            return [];
        }

        // One batched eligibility check for the whole list rather than one call
        // per subject - the difference between two queries and a hundred.
        var eligibility = await _prerequisiteService.CanEnrollManyAsync(
            studentId, outstanding.Select(o => o.CourseId), cancellationToken);

        return outstanding
            .Select(o => new MissingCourseDto(
                o.CourseId,
                o.CourseCode,
                o.CourseName,
                o.Credits,
                o.TermNo,
                IsEligibleNow: !eligibility.TryGetValue(o.CourseId, out var check) || check.CanEnroll))
            .ToList();
    }

    public async Task<IReadOnlyList<MissingCourseDto>> GetEligibleCoursesAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var missing = await GetMissingCoursesAsync(studentId, cancellationToken);

        return missing.Where(m => m.IsEligibleNow).ToList();
    }

    /// <summary>
    /// DISTINCT subjects the student has retaken.
    ///
    /// Counts subjects, not attempts: failing one subject three times is one
    /// retaken subject, which is what the regulation is about.
    /// </summary>
    private async Task<int> CountRetakenSubjectsAsync(int studentId, CancellationToken cancellationToken)
        => await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.AttemptNo > 1)
            .Select(e => e.CourseId)
            .Distinct()
            .CountAsync(cancellationToken);
}
