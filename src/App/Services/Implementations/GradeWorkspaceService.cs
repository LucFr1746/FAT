using Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Query projections for grade screens and deletion of an existing score.
/// </summary>
public sealed class GradeWorkspaceService : IGradeWorkspaceService
{
    private readonly FAT_DBContext _db;
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserContext _currentUser;

    public GradeWorkspaceService(
        FAT_DBContext db,
        IGradeService gradeService,
        ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _gradeService = gradeService ?? throw new ArgumentNullException(nameof(gradeService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<GradeCourseDto>> GetStudentGradesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View grades");

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .OrderByDescending(enrollment =>
                enrollment.Semester != null ? enrollment.Semester.DisplayOrder : int.MinValue)
            .ThenBy(enrollment => enrollment.Course != null
                ? enrollment.Course.CourseCode
                : string.Empty)
            .Select(enrollment => new
            {
                enrollment.EnrollmentId,
                enrollment.CourseId,
                enrollment.SemesterId,
                enrollment.FinalScore,
                enrollment.LetterGrade,
                enrollment.GradePoint,
                enrollment.Status,
                enrollment.AttemptNo,
                enrollment.IsCounted,
                CourseCode = enrollment.Course != null ? enrollment.Course.CourseCode : string.Empty,
                CourseName = enrollment.Course != null ? enrollment.Course.CourseName : string.Empty,
                Credits = enrollment.Course != null ? enrollment.Course.Credits : 0,
                SemesterCode = enrollment.Semester != null ? enrollment.Semester.SemesterCode : string.Empty,
                SemesterName = enrollment.Semester != null ? enrollment.Semester.SemesterName : string.Empty,
                SemesterDisplayOrder = enrollment.Semester != null
                    ? enrollment.Semester.DisplayOrder
                    : int.MinValue
            })
            .ToListAsync(cancellationToken);

        if (enrollments.Count == 0)
        {
            return [];
        }

        var courseIds = enrollments.Select(enrollment => enrollment.CourseId).Distinct().ToList();
        var enrollmentIds = enrollments.Select(enrollment => enrollment.EnrollmentId).ToList();

        var assessments = await _db.Assessments
            .AsNoTracking()
            .Where(assessment => courseIds.Contains(assessment.CourseId))
            .OrderBy(assessment => assessment.DisplayOrder)
            .ThenBy(assessment => assessment.Name)
            .Select(assessment => new
            {
                assessment.AssessmentId,
                assessment.CourseId,
                assessment.Name,
                assessment.Weight,
                assessment.MinScoreToPass,
                assessment.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        var gradeRows = await _db.Grades
            .AsNoTracking()
            .Where(grade => enrollmentIds.Contains(grade.EnrollmentId))
            .Select(grade => new
            {
                grade.EnrollmentId,
                grade.AssessmentId,
                grade.Score
            })
            .ToListAsync(cancellationToken);

        var scores = gradeRows.ToDictionary(
            row => (row.EnrollmentId, row.AssessmentId),
            row => row.Score);

        return enrollments.Select(enrollment => new GradeCourseDto(
            enrollment.EnrollmentId,
            enrollment.CourseId,
            enrollment.CourseCode,
            enrollment.CourseName,
            enrollment.Credits,
            enrollment.SemesterId,
            enrollment.SemesterCode,
            enrollment.SemesterName,
            enrollment.SemesterDisplayOrder,
            enrollment.FinalScore,
            enrollment.LetterGrade,
            enrollment.GradePoint,
            enrollment.Status,
            enrollment.AttemptNo,
            enrollment.IsCounted,
            assessments
                .Where(assessment => assessment.CourseId == enrollment.CourseId)
                .Select(assessment => new GradeAssessmentDto(
                    assessment.AssessmentId,
                    assessment.Name,
                    assessment.Weight,
                    assessment.MinScoreToPass,
                    assessment.DisplayOrder,
                    scores.TryGetValue(
                        (enrollment.EnrollmentId, assessment.AssessmentId),
                        out var score)
                            ? score
                            : null))
                .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<GradeStudentOptionDto>> GetStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Manage grades");

        return await _db.Students
            .AsNoTracking()
            .OrderBy(student => student.StudentCode)
            .Select(student => new GradeStudentOptionDto(
                student.StudentId,
                student.StudentCode,
                student.FullName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GradeEnrollmentOptionDto>> GetEnrollmentsAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Manage grades");

        return await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId
                                 && enrollment.Status != EnrollmentStatus.Withdrawn)
            .OrderByDescending(enrollment =>
                enrollment.Semester != null ? enrollment.Semester.DisplayOrder : int.MinValue)
            .ThenBy(enrollment => enrollment.Course != null
                ? enrollment.Course.CourseCode
                : string.Empty)
            .Select(enrollment => new GradeEnrollmentOptionDto(
                enrollment.EnrollmentId,
                enrollment.StudentId,
                enrollment.CourseId,
                enrollment.Course != null ? enrollment.Course.CourseCode : string.Empty,
                enrollment.Course != null ? enrollment.Course.CourseName : string.Empty,
                enrollment.Semester != null ? enrollment.Semester.SemesterCode : string.Empty,
                enrollment.FinalScore,
                enrollment.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GradeAssessmentDto>> GetAssessmentScoresAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Manage grades");

        var courseId = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.EnrollmentId == enrollmentId)
            .Select(enrollment => (int?)enrollment.CourseId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} was not found.");

        return await _db.Assessments
            .AsNoTracking()
            .Where(assessment => assessment.CourseId == courseId)
            .OrderBy(assessment => assessment.DisplayOrder)
            .ThenBy(assessment => assessment.Name)
            .Select(assessment => new GradeAssessmentDto(
                assessment.AssessmentId,
                assessment.Name,
                assessment.Weight,
                assessment.MinScoreToPass,
                assessment.DisplayOrder,
                assessment.Grades
                    .Where(grade => grade.EnrollmentId == enrollmentId)
                    .Select(grade => (decimal?)grade.Score)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteGradeAsync(
        int enrollmentId,
        int assessmentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Delete grades");

        var grade = await _db.Grades.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollmentId && item.AssessmentId == assessmentId,
            cancellationToken)
            ?? throw new InvalidOperationException("No recorded grade exists for the selected assessment.");

        _db.Grades.Remove(grade);
        await _db.SaveChangesAsync(cancellationToken);
        await _gradeService.RecalculateFinalScoreAsync(enrollmentId, cancellationToken);
    }
}
