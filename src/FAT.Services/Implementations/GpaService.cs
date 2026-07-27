using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

public sealed class GpaService(FatDbContext db) : IGpaService
{
    public async Task<decimal?> GetCumulativeGpaAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var rows = await CountedPasses(studentId).Select(e => new { e.FinalScore, e.Course!.Credits })
            .ToListAsync(cancellationToken);
        return Calculate(rows.Select(x => (x.FinalScore!.Value, x.Credits)));
    }

    public async Task<GpaSummaryDto> GetGpaSummaryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var cumulative = await GetCumulativeGpaAsync(studentId, cancellationToken);
        var classification = cumulative.HasValue
            ? AcademicRules.ClassifyGpa(cumulative.Value)
            : DegreeClassification.NotQualified;
        return new(cumulative, classification, AcademicRules.GetClassificationName(classification),
            await GetGpaBySemesterAsync(studentId, cancellationToken));
    }

    public async Task<IReadOnlyList<SemesterGpaDto>> GetGpaBySemesterAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .Select(e => new
            {
                e.SemesterId,
                e.Semester!.SemesterCode,
                e.Semester.DisplayOrder,
                e.Status,
                e.IsCounted,
                e.FinalScore,
                e.Course!.Credits
            }).ToListAsync(cancellationToken);
        return rows.GroupBy(x => new { x.SemesterId, x.SemesterCode, x.DisplayOrder })
            .OrderBy(g => g.Key.DisplayOrder)
            .Select(g => new SemesterGpaDto(g.Key.SemesterId, g.Key.SemesterCode, g.Key.DisplayOrder,
                Calculate(g.Where(x => x.Status == EnrollmentStatus.Passed && x.IsCounted && x.FinalScore.HasValue)
                    .Select(x => (x.FinalScore!.Value, x.Credits))),
                g.Where(x => x.Status == EnrollmentStatus.Passed && x.IsCounted).Sum(x => x.Credits)))
            .ToList();
    }

    public async Task<CreditSummaryDto> GetCreditSummaryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.AsNoTracking().Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                Required = s.Major!.RequiredCredits,
                Enrollments = s.Enrollments.Select(e => new { e.Status, e.IsCounted, e.Course!.Credits })
            })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("Student not found.");
        return new(student.Enrollments.Where(e => e.Status == EnrollmentStatus.Passed && e.IsCounted).Sum(e => e.Credits),
            student.Enrollments.Where(e => e.Status == EnrollmentStatus.Studying).Sum(e => e.Credits), student.Required);
    }

    private IQueryable<Domain.Entities.Enrollment> CountedPasses(int studentId) => db.Enrollments.AsNoTracking()
        .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Passed && e.IsCounted && e.FinalScore != null);

    internal static decimal? Calculate(IEnumerable<(decimal Score, int Credits)> rows)
    {
        var values = rows.ToList();
        var credits = values.Sum(x => x.Credits);
        return credits == 0 ? null : AcademicRules.RoundGpa(values.Sum(x => x.Score * x.Credits) / credits);
    }
}
