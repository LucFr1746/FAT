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
    public async Task<IReadOnlyList<Grade>> GetGradesAsync(int enrollmentId, CancellationToken cancellationToken = default) =>
        await db.Grades.AsNoTracking().Include(g => g.Assessment).Where(g => g.EnrollmentId == enrollmentId)
            .OrderBy(g => g.Assessment!.DisplayOrder).ToListAsync(cancellationToken);

    public async Task UpsertGradeAsync(int enrollmentId, int assessmentId, decimal score, CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 10.");
        var valid = await db.Enrollments.AnyAsync(e => e.EnrollmentId == enrollmentId &&
            db.Assessments.Any(a => a.AssessmentId == assessmentId && a.CourseId == e.CourseId), cancellationToken);
        if (!valid) throw new InvalidOperationException("Assessment does not belong to the enrolled course.");
        var grade = await db.Grades.SingleOrDefaultAsync(g => g.EnrollmentId == enrollmentId && g.AssessmentId == assessmentId, cancellationToken);
        if (grade is null) db.Grades.Add(new Grade { EnrollmentId = enrollmentId, AssessmentId = assessmentId, Score = score, UpdatedAt = DateTime.UtcNow });
        else { grade.Score = score; grade.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateFinalScoreAsync(enrollmentId, cancellationToken);
    }

    public async Task RecalculateFinalScoreAsync(int enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await db.Enrollments.Include(e => e.Course)!.ThenInclude(c => c!.Assessments)
            .Include(e => e.Grades).ThenInclude(g => g.Assessment).SingleOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");
        var assessments = enrollment.Course!.Assessments;
        if (assessments.Count == 0 || enrollment.Grades.Count != assessments.Count)
        {
            enrollment.FinalScore = null; enrollment.LetterGrade = null; enrollment.GradePoint = null;
            enrollment.Status = EnrollmentStatus.Studying;
        }
        else
        {
            var final = AcademicRules.RoundFinalScore(enrollment.Grades.Sum(g => g.Score * g.Assessment!.Weight));
            var scale = await db.GradeScales.AsNoTracking().SingleOrDefaultAsync(s => final >= s.MinScore && final < s.MaxScore, cancellationToken)
                ?? throw new InvalidOperationException("No grade-scale band covers the final score.");
            var passed = final >= AcademicRules.PassScore && enrollment.Grades.All(g =>
                !g.Assessment!.MinScoreToPass.HasValue || g.Score >= g.Assessment.MinScoreToPass.Value);
            enrollment.FinalScore = final; enrollment.LetterGrade = scale.LetterGrade; enrollment.GradePoint = scale.GradePoint;
            enrollment.Status = passed ? EnrollmentStatus.Passed : EnrollmentStatus.Failed;
        }
        enrollment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TranscriptDto> GetTranscriptAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .Select(e => new { Enrollment = e, e.Course, e.Semester }).ToListAsync(cancellationToken);
        var semesters = rows.GroupBy(x => x.Semester!).OrderBy(g => g.Key.DisplayOrder).Select(g =>
        {
            var items = g.OrderBy(x => x.Course!.CourseCode).Select(x => new TranscriptItemDto(x.Enrollment.EnrollmentId,
                x.Course!.CourseCode, x.Course.CourseName, x.Course.Credits, x.Enrollment.FinalScore, x.Enrollment.LetterGrade,
                x.Enrollment.GradePoint, x.Enrollment.Status, x.Enrollment.IsCounted, x.Enrollment.AttemptNo)).ToList();
            var counted = items.Where(x => x.Status == EnrollmentStatus.Passed && x.IsCounted && x.FinalScore.HasValue).ToList();
            var credits = counted.Sum(x => x.Credits);
            decimal? gpa = credits == 0
                ? null
                : AcademicRules.RoundGpa(counted.Sum(x => x.FinalScore!.Value * x.Credits) / credits);
            return new SemesterTranscriptDto(g.Key.SemesterId, g.Key.SemesterCode, g.Key.SemesterName, g.Key.DisplayOrder,
                g.Key.IsCurrent, items, gpa, credits);
        }).ToList();
        return new(student.StudentId, student.StudentCode, student.FullName, semesters);
    }

    public async Task<int> EnrollAsync(int studentId, int courseId, int semesterId, CancellationToken cancellationToken = default)
    {
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId && e.SemesterId == semesterId, cancellationToken))
            throw new InvalidOperationException("Student is already enrolled in this course for the semester.");
        var attempts = await db.Enrollments.Where(e => e.StudentId == studentId && e.CourseId == courseId).ToListAsync(cancellationToken);
        foreach (var old in attempts) old.IsCounted = false;
        var enrollment = new Enrollment { StudentId = studentId, CourseId = courseId, SemesterId = semesterId,
            AttemptNo = attempts.Count + 1, IsCounted = true, CreatedAt = DateTime.UtcNow };
        db.Enrollments.Add(enrollment); await db.SaveChangesAsync(cancellationToken); return enrollment.EnrollmentId;
    }

    public async Task WithdrawAsync(int enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await db.Enrollments.FindAsync([enrollmentId], cancellationToken) ?? throw new KeyNotFoundException("Enrollment not found.");
        enrollment.Status = EnrollmentStatus.Withdrawn; enrollment.FinalScore = null; enrollment.LetterGrade = null;
        enrollment.GradePoint = null; enrollment.IsCounted = false; enrollment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
