using FAT.Data;
using Microsoft.EntityFrameworkCore;

namespace FAT.App.ViewModels.GradeGpa;

public sealed record StudentOption(int StudentId, string Label);
public sealed record EnrollmentOption(int EnrollmentId, int StudentId, string Label, decimal? FinalScore, string Status);
public sealed record AssessmentScore(int AssessmentId, string Name, decimal Weight, decimal? MinimumScore, decimal? Score)
{
    public string WeightLabel => $"{Weight:P0}";
}

/// <summary>Read model used only by the grade-entry workflow; all writes remain in IGradeService.</summary>
public sealed class GradeWorkspaceService(FatDbContext db)
{
    public async Task<IReadOnlyList<StudentOption>> GetStudentsAsync(CancellationToken token = default) =>
        await db.Students.AsNoTracking().OrderBy(s => s.StudentCode)
            .Select(s => new StudentOption(s.StudentId, s.StudentCode + " — " + s.FullName)).ToListAsync(token);

    public async Task<IReadOnlyList<EnrollmentOption>> GetEnrollmentsAsync(int studentId, CancellationToken token = default) =>
        await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.Semester!.DisplayOrder).ThenBy(e => e.Course!.CourseCode)
            .Select(e => new EnrollmentOption(e.EnrollmentId, e.StudentId,
                e.Semester!.SemesterCode + " · " + e.Course!.CourseCode + " — " + e.Course.CourseName,
                e.FinalScore, e.Status.ToString())).ToListAsync(token);

    public async Task<IReadOnlyList<AssessmentScore>> GetAssessmentScoresAsync(int enrollmentId, CancellationToken token = default)
    {
        var courseId = await db.Enrollments.Where(e => e.EnrollmentId == enrollmentId)
            .Select(e => (int?)e.CourseId).SingleOrDefaultAsync(token)
            ?? throw new KeyNotFoundException("Enrollment not found.");
        return await db.Assessments.AsNoTracking().Where(a => a.CourseId == courseId).OrderBy(a => a.DisplayOrder)
            .Select(a => new AssessmentScore(a.AssessmentId, a.Name, a.Weight, a.MinScoreToPass,
                a.Grades.Where(g => g.EnrollmentId == enrollmentId).Select(g => (decimal?)g.Score).SingleOrDefault()))
            .ToListAsync(token);
    }
}
