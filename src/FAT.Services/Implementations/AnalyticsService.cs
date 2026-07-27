using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

public sealed class AnalyticsService(FatDbContext db, IGpaService gpa, IGradeService grades) : IAnalyticsService
{
    public async Task<IReadOnlyList<GradeDistributionDto>> GetGradeDistributionAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var letters = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.IsCounted &&
            (e.Status == EnrollmentStatus.Passed || e.Status == EnrollmentStatus.Failed) && e.LetterGrade != null)
            .Select(e => e.LetterGrade!).ToListAsync(cancellationToken);
        return letters.GroupBy(x => x).OrderByDescending(g => g.Key).Select(x =>
            new GradeDistributionDto(x.Key, x.Count(), letters.Count == 0 ? 0 : Math.Round(100m * x.Count() / letters.Count, 1))).ToList();
    }

    public async Task<IReadOnlyList<GpaTrendPointDto>> GetGpaTrendAsync(int studentId, CancellationToken cancellationToken = default) =>
        (await gpa.GetGpaBySemesterAsync(studentId, cancellationToken)).Select(x =>
            new GpaTrendPointDto(x.SemesterCode, x.DisplayOrder, x.Gpa, x.EarnedCredits)).ToList();

    public Task<IReadOnlyList<CourseHighlightDto>> GetTopCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default) =>
        Highlights(studentId, take, false, cancellationToken);

    public Task<IReadOnlyList<CourseHighlightDto>> GetWeakestCoursesAsync(int studentId, int take = 5, CancellationToken cancellationToken = default) =>
        Highlights(studentId, take, true, cancellationToken);

    public async Task<IReadOnlyList<AcademicWarningDto>> GetAcademicWarningsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var transcript = await grades.GetTranscriptAsync(studentId, cancellationToken);
        return transcript.Semesters.Select(s => new { Semester = s, Failed = s.Items.Count(i => i.Status == EnrollmentStatus.Failed) })
            .Where(x => (x.Semester.SemesterGpa.HasValue && x.Semester.SemesterGpa < AcademicRules.AcademicWarningGpaThreshold) ||
                x.Failed >= AcademicRules.AcademicWarningFailedCourseCount)
            .Select(x => new AcademicWarningDto(x.Semester.SemesterCode,
                x.Failed >= AcademicRules.AcademicWarningFailedCourseCount ? $"Failed {x.Failed} courses" : "Semester GPA below 5.0",
                x.Semester.SemesterGpa, x.Failed)).ToList();
    }

    public async Task<DashboardDto> GetDashboardAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking().Where(s => s.StudentId == studentId)
            .Select(s => new { s.StudentCode, s.FullName, MajorName = s.Major!.MajorName }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");
        var summary = await gpa.GetGpaSummaryAsync(studentId, cancellationToken);
        var credits = await gpa.GetCreditSummaryAsync(studentId, cancellationToken);
        var transcript = await grades.GetTranscriptAsync(studentId, cancellationToken);
        var all = transcript.Semesters.SelectMany(s => s.Items).ToList();
        var current = transcript.Semesters.FirstOrDefault(s => s.IsCurrent);
        return new(student.StudentCode, student.FullName, student.MajorName, summary.CumulativeGpa, summary.Classification,
            summary.ClassificationName, credits.EarnedCredits, credits.RequiredCredits, credits.InProgressCredits,
            credits.CompletionPercent, all.Count(x => x.Status == EnrollmentStatus.Passed && x.IsCounted),
            all.Count(x => x.Status == EnrollmentStatus.Failed), all.Count(x => x.Status == EnrollmentStatus.Studying),
            current?.SemesterCode, await GetGpaTrendAsync(studentId, cancellationToken),
            await GetGradeDistributionAsync(studentId, cancellationToken), current?.Items ?? [],
            await GetAcademicWarningsAsync(studentId, cancellationToken));
    }

    private async Task<IReadOnlyList<CourseHighlightDto>> Highlights(int studentId, int take, bool ascending, CancellationToken token)
    {
        if (take < 0) throw new ArgumentOutOfRangeException(nameof(take));
        var query = db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.IsCounted && e.FinalScore != null);
        query = ascending ? query.OrderBy(e => e.FinalScore) : query.OrderByDescending(e => e.FinalScore);
        return await query.Take(take).Select(e => new CourseHighlightDto(e.Course!.CourseCode, e.Course.CourseName,
            e.FinalScore!.Value, e.LetterGrade)).ToListAsync(token);
    }
}
