using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>
/// GPA and credit totals.
///
/// THE RULES, all in one place because the dashboard, the transcript, the
/// progress screen and the prediction screen must all produce the same number:
///
///   1. Only Status = Passed AND IsCounted = true contributes.
///   2. Weighted by credits: SUM(FinalScore * Credits) / SUM(Credits).
///   3. Failed, Withdrawn and Studying contribute to NEITHER side of that
///      fraction - not even to the denominator.
///   4. A retaken subject counts ONCE, through its latest attempt. That is
///      exactly what IsCounted encodes; ignoring it is the classic bug that
///      produces a suspiciously high GPA.
///   5. Subjects with CountsTowardGpa = false (physical education, orientation)
///      earn CREDITS but never enter the GPA.
///
/// Rule 5 is the one added with the FLM catalog: 33 of the 135 subjects are
/// marked "Tính GPA = Không", and counting them would move every GPA.
/// </summary>
public sealed class GpaService : IGpaService
{
    private readonly FatDbContext _db;

    public GpaService(FatDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<decimal?> GetCumulativeGpaAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var rows = await GetGpaRowsAsync(studentId, cancellationToken);

        return CalculateGpa(rows);
    }

    public async Task<GpaSummaryDto> GetGpaSummaryAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var bySemester = await GetGpaBySemesterAsync(studentId, cancellationToken);
        var cumulative = await GetCumulativeGpaAsync(studentId, cancellationToken);
        var classification = AcademicRules.ClassifyGpa(cumulative ?? 0m);

        return new GpaSummaryDto(
            cumulative,
            classification,
            AcademicRules.GetClassificationName(classification),
            bySemester);
    }

    public async Task<IReadOnlyList<SemesterGpaDto>> GetGpaBySemesterAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        // Every passed attempt, including the ones that no longer count toward
        // the GPA: earned credits and GPA answer different questions, and the
        // two are separated below rather than in two round trips.
        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Passed)
            .Select(e => new
            {
                e.SemesterId,
                e.Semester!.SemesterCode,
                e.Semester.DisplayOrder,
                e.IsCounted,
                e.FinalScore,
                e.Course!.Credits,
                e.Course.CountsTowardGpa
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.SemesterId, r.SemesterCode, r.DisplayOrder })
            .OrderBy(g => g.Key.DisplayOrder)
            .Select(g => new SemesterGpaDto(
                g.Key.SemesterId,
                g.Key.SemesterCode,
                g.Key.DisplayOrder,
                CalculateGpa(g
                    .Where(r => r.IsCounted && r.CountsTowardGpa && r.FinalScore.HasValue)
                    .Select(r => (r.FinalScore!.Value, r.Credits))),
                // Credits are earned by every passed attempt that still counts,
                // whether or not the subject feeds the GPA.
                g.Where(r => r.IsCounted).Sum(r => r.Credits)))
            .ToList();
    }

    public async Task<CreditSummaryDto> GetCreditSummaryAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => new { e.Status, e.IsCounted, e.Course!.Credits })
            .ToListAsync(cancellationToken);

        var requiredCredits = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => s.Major!.RequiredCredits)
            .FirstOrDefaultAsync(cancellationToken);

        return new CreditSummaryDto(
            EarnedCredits: rows.Where(r => r.Status == EnrollmentStatus.Passed && r.IsCounted).Sum(r => r.Credits),
            InProgressCredits: rows.Where(r => r.Status == EnrollmentStatus.Studying).Sum(r => r.Credits),
            RequiredCredits: requiredCredits);
    }

    /// <summary>
    /// The score/credit pairs that make up a student's GPA.
    /// Internal so the prediction service can build on exactly the same set
    /// rather than writing a second, subtly different query.
    /// </summary>
    internal async Task<IReadOnlyList<(decimal Score, int Credits)>> GetGpaRowsAsync(
        int studentId, CancellationToken cancellationToken)
        => await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                        && e.Status == EnrollmentStatus.Passed
                        && e.IsCounted
                        && e.Course!.CountsTowardGpa
                        && e.FinalScore != null)
            .Select(e => new ValueTuple<decimal, int>(e.FinalScore!.Value, e.Course!.Credits))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Credit-weighted average.
    ///
    /// Returns NULL, not zero, when nothing qualifies: a new student has no GPA,
    /// and showing 0.00 would read as failure and would drag any average that
    /// included it to the floor.
    /// </summary>
    internal static decimal? CalculateGpa(IEnumerable<(decimal Score, int Credits)> rows)
    {
        var materialised = rows.ToList();

        if (materialised.Count == 0)
        {
            return null;
        }

        var totalCredits = materialised.Sum(r => r.Credits);

        // Zero-credit subjects can qualify (Kỳ 0 orientation), and dividing by
        // their total would throw.
        if (totalCredits <= 0)
        {
            return null;
        }

        var weightedTotal = materialised.Sum(r => r.Score * r.Credits);

        return AcademicRules.RoundGpa(weightedTotal / totalCredits);
    }
}
