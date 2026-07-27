using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Entities;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

public sealed class GradeService(FatDbContext db) : IGradeService
{
    public async Task<IReadOnlyList<Grade>> GetGradesAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        return await db.Grades
            .AsNoTracking()
            .Include(grade => grade.Assessment)
            .Where(grade => grade.EnrollmentId == enrollmentId)
            .OrderBy(grade => grade.Assessment!.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertGradeAsync(
        int enrollmentId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                "Score must be between 0 and 10.");
        }

        var assessmentBelongsToCourse = await db.Enrollments.AnyAsync(
            enrollment => enrollment.EnrollmentId == enrollmentId &&
                db.Assessments.Any(assessment =>
                    assessment.AssessmentId == assessmentId &&
                    assessment.CourseId == enrollment.CourseId),
            cancellationToken);

        if (!assessmentBelongsToCourse)
        {
            throw new InvalidOperationException(
                "Assessment does not belong to the enrolled course.");
        }

        var grade = await db.Grades.SingleOrDefaultAsync(
            item => item.EnrollmentId == enrollmentId &&
                item.AssessmentId == assessmentId,
            cancellationToken);

        if (grade is null)
        {
            db.Grades.Add(new Grade
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

        await db.SaveChangesAsync(cancellationToken);
        await RecalculateFinalScoreAsync(enrollmentId, cancellationToken);
    }

    public async Task RecalculateFinalScoreAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await db.Enrollments
            .Include(item => item.Course)!
            .ThenInclude(course => course!.Assessments)
            .Include(item => item.Grades)
            .ThenInclude(grade => grade.Assessment)
            .SingleOrDefaultAsync(
                item => item.EnrollmentId == enrollmentId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        var assessments = enrollment.Course!.Assessments;

        if (assessments.Count == 0 || enrollment.Grades.Count != assessments.Count)
        {
            enrollment.FinalScore = null;
            enrollment.LetterGrade = null;
            enrollment.GradePoint = null;
            enrollment.Status = EnrollmentStatus.Studying;
        }
        else
        {
            var finalScore = AcademicRules.RoundFinalScore(
                enrollment.Grades.Sum(grade =>
                    grade.Score * grade.Assessment!.Weight));

            var scale = await db.GradeScales
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => finalScore >= item.MinScore &&
                        finalScore < item.MaxScore,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "No grade-scale band covers the final score.");

            var passed = finalScore >= AcademicRules.PassScore &&
                enrollment.Grades.All(grade =>
                    !grade.Assessment!.MinScoreToPass.HasValue ||
                    grade.Score >= grade.Assessment.MinScoreToPass.Value);

            enrollment.FinalScore = finalScore;
            enrollment.LetterGrade = scale.LetterGrade;
            enrollment.GradePoint = scale.GradePoint;
            enrollment.Status = passed
                ? EnrollmentStatus.Passed
                : EnrollmentStatus.Failed;
        }

        enrollment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TranscriptDto> GetTranscriptAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await db.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.StudentId == studentId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var rows = await db.Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.StudentId == studentId)
            .Select(enrollment => new
            {
                Enrollment = enrollment,
                enrollment.Course,
                enrollment.Semester
            })
            .ToListAsync(cancellationToken);

        var semesters = rows
            .GroupBy(row => row.Semester!)
            .OrderBy(group => group.Key.DisplayOrder)
            .Select(group =>
            {
                var items = group
                    .OrderBy(row => row.Course!.CourseCode)
                    .Select(row => new TranscriptItemDto(
                        row.Enrollment.EnrollmentId,
                        row.Course!.CourseCode,
                        row.Course.CourseName,
                        row.Course.Credits,
                        row.Enrollment.FinalScore,
                        row.Enrollment.LetterGrade,
                        row.Enrollment.GradePoint,
                        row.Enrollment.Status,
                        row.Enrollment.IsCounted,
                        row.Enrollment.AttemptNo))
                    .ToList();

                var counted = items
                    .Where(item => item.Status == EnrollmentStatus.Passed &&
                        item.IsCounted &&
                        item.FinalScore.HasValue)
                    .ToList();

                var earnedCredits = counted.Sum(item => item.Credits);

                decimal? semesterGpa = earnedCredits == 0
                    ? null
                    : AcademicRules.RoundGpa(
                        counted.Sum(item =>
                            item.FinalScore!.Value * item.Credits) /
                        earnedCredits);

                return new SemesterTranscriptDto(
                    group.Key.SemesterId,
                    group.Key.SemesterCode,
                    group.Key.SemesterName,
                    group.Key.DisplayOrder,
                    group.Key.IsCurrent,
                    items,
                    semesterGpa,
                    earnedCredits);
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
        var targetSemesterOrder = await db.Semesters
            .Where(semester => semester.SemesterId == semesterId)
            .Select(semester => (int?)semester.DisplayOrder)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Semester not found.");

        if (!await db.Students.AnyAsync(
                student => student.StudentId == studentId,
                cancellationToken))
        {
            throw new KeyNotFoundException("Student not found.");
        }

        if (!await db.Courses.AnyAsync(
                course => course.CourseId == courseId && course.IsActive,
                cancellationToken))
        {
            throw new KeyNotFoundException("Active course not found.");
        }

        var duplicate = await db.Enrollments.AnyAsync(
            enrollment => enrollment.StudentId == studentId &&
                enrollment.CourseId == courseId &&
                enrollment.SemesterId == semesterId,
            cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "Student is already enrolled in this course for the semester.");
        }

        var requirements = await db.Prerequisites
            .AsNoTracking()
            .Where(requirement => requirement.CourseId == courseId)
            .Select(requirement => new
            {
                requirement.RequiredCourseId,
                requirement.Type
            })
            .ToListAsync(cancellationToken);

        foreach (var requirement in requirements)
        {
            var satisfied = await db.Enrollments.AnyAsync(
                enrollment => enrollment.StudentId == studentId &&
                    enrollment.CourseId == requirement.RequiredCourseId &&
                    ((requirement.Type == PrerequisiteType.Prerequisite &&
                      enrollment.Status == EnrollmentStatus.Passed &&
                      enrollment.Semester!.DisplayOrder < targetSemesterOrder) ||
                     (requirement.Type == PrerequisiteType.Corequisite &&
                      enrollment.Status != EnrollmentStatus.Withdrawn &&
                      enrollment.Semester!.DisplayOrder <= targetSemesterOrder)),
                cancellationToken);

            if (!satisfied)
            {
                throw new InvalidOperationException(
                    "Course prerequisites have not been satisfied.");
            }
        }

        var previousAttempts = await db.Enrollments
            .Where(enrollment => enrollment.StudentId == studentId &&
                enrollment.CourseId == courseId)
            .ToListAsync(cancellationToken);

        foreach (var previousAttempt in previousAttempts)
        {
            previousAttempt.IsCounted = false;
        }

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            SemesterId = semesterId,
            AttemptNo = previousAttempts.Count + 1,
            IsCounted = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(cancellationToken);

        return enrollment.EnrollmentId;
    }

    public async Task WithdrawAsync(
        int enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = await db.Enrollments.FindAsync(
            [enrollmentId],
            cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.FinalScore = null;
        enrollment.LetterGrade = null;
        enrollment.GradePoint = null;
        enrollment.IsCounted = false;
        enrollment.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
