using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Grade entry, final-score settlement and transcript aggregation.
/// </summary>
public sealed class GradeService : IGradeService
{
    private readonly FAT_DBContext _db;
    private readonly IGpaService _gpaService;
    private readonly IPrerequisiteService _prerequisiteService;
    private readonly ICurrentUserContext _currentUser;

    public GradeService(
        FAT_DBContext db,
        IGpaService gpaService,
        IPrerequisiteService prerequisiteService,
        ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
        _prerequisiteService = prerequisiteService ?? throw new ArgumentNullException(nameof(prerequisiteService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<Grade>> GetGradesAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var studentId = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.EnrollmentId == enrollmentId)
            .Select(enrollment => (int?)enrollment.StudentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} was not found.");

        _currentUser.RequireSelfOrAdmin(studentId, "View component grades");

        return await _db.Grades
            .AsNoTracking()
            .Include(grade => grade.Assessment)
            .Where(grade => grade.EnrollmentId == enrollmentId)
            .OrderBy(grade => grade.Assessment != null ? grade.Assessment.DisplayOrder : int.MaxValue)
            .ThenBy(grade => grade.Assessment != null ? grade.Assessment.Name : string.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertGradeAsync(
        int enrollmentId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Add or edit grades");
        GradeCalculation.ValidateScore(score);

        var enrollment = await _db.Enrollments
            .AsNoTracking()
            .Where(item => item.EnrollmentId == enrollmentId)
            .Select(item => new { item.CourseId, item.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} was not found.");

        if (enrollment.Status == EnrollmentStatus.Withdrawn)
        {
            throw new InvalidOperationException("Grades cannot be recorded for a withdrawn enrollment.");
        }

        var assessmentExists = await _db.Assessments.AnyAsync(
            assessment => assessment.AssessmentId == assessmentId
                          && assessment.CourseId == enrollment.CourseId,
            cancellationToken);

        if (!assessmentExists)
        {
            throw new InvalidOperationException(
                "The selected assessment does not belong to the enrolled course.");
        }

        var grade = await _db.Grades.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollmentId && item.AssessmentId == assessmentId,
            cancellationToken);

        if (grade is null)
        {
            _db.Grades.Add(new Grade
            {
                EnrollmentId = enrollmentId,
                AssessmentId = assessmentId,
                Score = score,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            grade.Score = score;
            grade.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateFinalScoreAsync(enrollmentId, cancellationToken);
    }

    public async Task RecalculateFinalScoreAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Recalculate final grades");

        var enrollment = await _db.Enrollments
            .Include(item => item.Course)!
            .ThenInclude(course => course!.Assessments)
            .Include(item => item.Grades)
            .SingleOrDefaultAsync(item => item.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} was not found.");

        if (enrollment.Status == EnrollmentStatus.Withdrawn)
        {
            throw new InvalidOperationException("A withdrawn enrollment cannot be recalculated.");
        }

        var assessments = enrollment.Course?.Assessments
            .OrderBy(assessment => assessment.DisplayOrder)
            .ToList() ?? [];

        var scores = enrollment.Grades.ToDictionary(
            grade => grade.AssessmentId,
            grade => grade.Score);

        var finalScore = GradeCalculation.CalculateFinalScore(
            assessments.Select(assessment => (
                assessment.Weight,
                scores.TryGetValue(assessment.AssessmentId, out var score)
                    ? (decimal?)score
                    : null)));

        if (!finalScore.HasValue)
        {
            enrollment.FinalScore = null;
            enrollment.LetterGrade = null;
            enrollment.GradePoint = null;
            enrollment.Status = EnrollmentStatus.Studying;
        }
        else
        {
            var scale = await _db.GradeScales
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => finalScore.Value >= item.MinScore && finalScore.Value < item.MaxScore,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"No grade-scale band covers the final score {finalScore.Value:N1}.");

            var passScore = enrollment.Course?.MinAvgMarkToPass ?? AcademicRules.PassScore;
            var componentMinimumsMet = assessments.All(assessment =>
                !assessment.MinScoreToPass.HasValue
                || (scores.TryGetValue(assessment.AssessmentId, out var score)
                    && score >= assessment.MinScoreToPass.Value));

            enrollment.FinalScore = finalScore.Value;
            enrollment.LetterGrade = scale.LetterGrade;
            enrollment.GradePoint = scale.GradePoint;
            enrollment.Status = finalScore.Value >= passScore && componentMinimumsMet
                ? EnrollmentStatus.Passed
                : EnrollmentStatus.Failed;
        }

        enrollment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TranscriptDto> GetTranscriptAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "View transcript");

        var student = await _db.Students
            .AsNoTracking()
            .Where(item => item.StudentId == studentId)
            .Select(item => new { item.StudentId, item.StudentCode, item.FullName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Student {studentId} was not found.");

        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .Select(enrollment => new
            {
                enrollment.EnrollmentId,
                enrollment.FinalScore,
                enrollment.LetterGrade,
                enrollment.GradePoint,
                enrollment.Status,
                enrollment.IsCounted,
                enrollment.AttemptNo,
                CourseCode = enrollment.Course != null ? enrollment.Course.CourseCode : string.Empty,
                CourseName = enrollment.Course != null ? enrollment.Course.CourseName : string.Empty,
                Credits = enrollment.Course != null ? enrollment.Course.Credits : 0,
                SemesterId = enrollment.Semester != null ? enrollment.Semester.SemesterId : enrollment.SemesterId,
                SemesterCode = enrollment.Semester != null ? enrollment.Semester.SemesterCode : string.Empty,
                SemesterName = enrollment.Semester != null ? enrollment.Semester.SemesterName : string.Empty,
                DisplayOrder = enrollment.Semester != null ? enrollment.Semester.DisplayOrder : int.MaxValue,
                IsCurrent = enrollment.Semester != null && enrollment.Semester.IsCurrent
            })
            .ToListAsync(cancellationToken);

        var semesterGpas = (await _gpaService.GetGpaBySemesterAsync(studentId, cancellationToken))
            .ToDictionary(item => item.SemesterId);

        var semesters = rows
            .GroupBy(row => new
            {
                row.SemesterId,
                row.SemesterCode,
                row.SemesterName,
                row.DisplayOrder,
                row.IsCurrent
            })
            .OrderBy(group => group.Key.DisplayOrder)
            .Select(group =>
            {
                var items = group
                    .OrderBy(row => row.CourseCode)
                    .ThenBy(row => row.AttemptNo)
                    .Select(row => new TranscriptItemDto(
                        row.EnrollmentId,
                        row.CourseCode,
                        row.CourseName,
                        row.Credits,
                        row.FinalScore,
                        row.LetterGrade,
                        row.GradePoint,
                        row.Status,
                        row.IsCounted,
                        row.AttemptNo))
                    .ToList();

                semesterGpas.TryGetValue(group.Key.SemesterId, out var semesterGpa);

                return new SemesterTranscriptDto(
                    group.Key.SemesterId,
                    group.Key.SemesterCode,
                    group.Key.SemesterName,
                    group.Key.DisplayOrder,
                    group.Key.IsCurrent,
                    items,
                    semesterGpa?.Gpa,
                    semesterGpa?.EarnedCredits ?? 0);
            })
            .ToList();

        return new TranscriptDto(
            student.StudentId,
            student.StudentCode,
            student.FullName,
            semesters);
    }

    public async Task<int> EnrollAsync(
        int studentId,
        int courseId,
        int semesterId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Enroll in a course");

        if (!await _db.Students.AnyAsync(student => student.StudentId == studentId, cancellationToken))
        {
            throw new InvalidOperationException($"Student {studentId} was not found.");
        }

        if (!await _db.Courses.AnyAsync(
                course => course.CourseId == courseId && course.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException($"Active course {courseId} was not found.");
        }

        if (!await _db.Semesters.AnyAsync(
                semester => semester.SemesterId == semesterId,
                cancellationToken))
        {
            throw new InvalidOperationException($"Semester {semesterId} was not found.");
        }

        if (await _db.Enrollments.AnyAsync(
                enrollment => enrollment.StudentId == studentId
                              && enrollment.CourseId == courseId
                              && enrollment.SemesterId == semesterId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The student is already enrolled in this course for the selected semester.");
        }

        var prerequisiteCheck = await _prerequisiteService.CanEnrollAsync(
            studentId,
            courseId,
            cancellationToken);

        if (!prerequisiteCheck.CanEnroll)
        {
            throw new InvalidOperationException("Course prerequisites have not been satisfied.");
        }

        var previousAttempts = await _db.Enrollments
            .Where(enrollment => enrollment.StudentId == studentId && enrollment.CourseId == courseId)
            .ToListAsync(cancellationToken);

        foreach (var previousAttempt in previousAttempts)
        {
            previousAttempt.IsCounted = false;
            previousAttempt.UpdatedAt = DateTime.UtcNow;
        }

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            SemesterId = semesterId,
            Status = EnrollmentStatus.Studying,
            AttemptNo = previousAttempts.Count == 0
                ? 1
                : previousAttempts.Max(attempt => attempt.AttemptNo) + 1,
            IsCounted = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync(cancellationToken);
        return enrollment.EnrollmentId;
    }

    public async Task WithdrawAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await _db.Enrollments
            .SingleOrDefaultAsync(item => item.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Enrollment {enrollmentId} was not found.");

        _currentUser.RequireSelfOrAdmin(enrollment.StudentId, "Withdraw from a course");

        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.FinalScore = null;
        enrollment.LetterGrade = null;
        enrollment.GradePoint = null;
        enrollment.IsCounted = false;
        enrollment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
