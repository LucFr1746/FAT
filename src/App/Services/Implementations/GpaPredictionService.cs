using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// GPA forecasting and the retake penalty.
///
/// Deliberately builds on <see cref="GpaService"/>'s own row set and averaging
/// helper rather than writing a second query. A prediction screen that computed
/// the GPA slightly differently from the dashboard would be worse than having no
/// prediction at all - and the difference would only show up at a boundary,
/// where nobody looks until it matters.
/// </summary>
public sealed class GpaPredictionService : IGpaPredictionService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly GpaService _gpaService;

    public GpaPredictionService(
        FAT_DBContext db,
        ICurrentUserContext currentUser,
        IGpaService gpaService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

        // The concrete type is needed for the internal row set that keeps this
        // service and the real GPA on identical inputs.
        _gpaService = gpaService as GpaService
            ?? throw new ArgumentException(
                "GpaPredictionService requires the concrete GpaService.", nameof(gpaService));
    }

    public async Task<GpaPredictionDto> PredictAsync(
        int studentId,
        IEnumerable<PlannedGradeDto>? plannedGrades = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Dự đoán GPA");

        var actual = await _gpaService.GetGpaRowsAsync(studentId, cancellationToken);
        var currentGpa = GpaService.CalculateGpa(actual);

        var planned = plannedGrades?.ToList() ?? [];
        var projectedRows = actual.ToList();

        if (planned.Count > 0)
        {
            var plannedCourseIds = planned.Select(p => p.CourseId).Distinct().ToList();

            // Only subjects that feed the GPA are projected. Physical education
            // earns credits but must not move the average.
            var courses = await _db.Courses
                .AsNoTracking()
                .Where(c => plannedCourseIds.Contains(c.CourseId))
                .Select(c => new { c.CourseId, c.Credits, c.CountsTowardGpa })
                .ToDictionaryAsync(c => c.CourseId, cancellationToken);

            foreach (var plan in planned)
            {
                if (!courses.TryGetValue(plan.CourseId, out var course) || !course.CountsTowardGpa)
                {
                    continue;
                }

                if (plan.ExpectedScore < 0m || plan.ExpectedScore > 10m)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(plannedGrades),
                        $"Điểm dự kiến của môn học phải nằm trong khoảng 0 đến 10.");
                }

                // A predicted Failed result still enters the GPA with its real
                // score and credits. It simply earns no completed credits.
                projectedRows.Add((plan.ExpectedScore, course.Credits));
            }
        }

        var predictedGpa = GpaService.CalculateGpa(projectedRows) ?? 0m;

        var retakenSubjects = await CountRetakenSubjectsAsync(studentId, cancellationToken);
        var baseClassification = AcademicRules.ClassifyGpa(predictedGpa);
        var adjusted = GraduationRules.Demote(
            baseClassification, GraduationRules.GetDemotionSteps(retakenSubjects));

        var credits = await _gpaService.GetCreditSummaryAsync(studentId, cancellationToken);
        var plannedCredits = await GetPlannedCreditsAsync(planned, cancellationToken);

        return new GpaPredictionDto(
            CurrentGpa: currentGpa,
            PredictedGpa: predictedGpa,
            RetakenSubjectCount: retakenSubjects,
            BaseClassification: baseClassification,
            BaseClassificationName: AcademicRules.GetClassificationName(baseClassification),
            AdjustedClassification: adjusted,
            AdjustedClassificationName: AcademicRules.GetClassificationName(adjusted),
            ProjectedEarnedCredits: credits.EarnedCredits + plannedCredits,
            RequiredCredits: credits.RequiredCredits,
            DemotionReason: GraduationRules.DescribeDemotion(retakenSubjects));
    }

    public async Task<int> SaveSnapshotAsync(
        int studentId,
        GpaPredictionDto prediction,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Lưu kết quả dự đoán GPA");

        if (!await _db.Students.AnyAsync(s => s.StudentId == studentId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy sinh viên có mã định danh {studentId}.");
        }

        var entity = new GradePrediction
        {
            StudentId = studentId,
            CurrentGpa = prediction.CurrentGpa,
            // Clamped to satisfy CK_GradePrediction_Gpa: a caller passing an
            // out-of-range value should not abort the save with a constraint
            // error nobody can act on.
            PredictedGpa = Math.Clamp(prediction.PredictedGpa, 0m, 10m),
            RetakeCount = Math.Max(0, prediction.RetakenSubjectCount),
            BaseClassification = prediction.BaseClassification,
            AdjustedClassification = prediction.AdjustedClassification,
            Note = note ?? prediction.DemotionReason,
            CreatedAt = DateTime.UtcNow
        };

        _db.GradePredictions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.GradePredictionId;
    }

    public async Task<IReadOnlyList<GpaPredictionDto>> GetHistoryAsync(
        int studentId, int take = 10, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem lịch sử dự đoán GPA");

        var requiredCredits = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => s.Major!.RequiredCredits)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await _db.GradePredictions
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.GradePredictionId)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

        return rows
            .Select(p => new GpaPredictionDto(
                CurrentGpa: p.CurrentGpa,
                PredictedGpa: p.PredictedGpa,
                RetakenSubjectCount: p.RetakeCount,
                BaseClassification: p.BaseClassification,
                BaseClassificationName: AcademicRules.GetClassificationName(p.BaseClassification),
                AdjustedClassification: p.AdjustedClassification,
                AdjustedClassificationName: AcademicRules.GetClassificationName(p.AdjustedClassification),
                // Not stored on the snapshot: it is a live figure, and showing a
                // stale one next to a historical GPA would be misleading.
                ProjectedEarnedCredits: 0,
                RequiredCredits: requiredCredits,
                DemotionReason: p.Note))
            .ToList();
    }

    /// <summary>
    /// DISTINCT subjects whose current official final score is exactly zero.
    /// This is the "môn học lại" rule used by the prediction screen. A registered
    /// student's aggregate is trusted only after every component grade exists,
    /// matching the GPA calculation and avoiding placeholder zeroes.
    /// </summary>
    private async Task<int> CountRetakenSubjectsAsync(int studentId, CancellationToken cancellationToken)
        => await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                        && e.IsCounted
                        && e.FinalScore == 0m
                        && (e.Student!.CurrentTermNo == null
                            || (e.Course!.Assessments.Any()
                                && e.Course.Assessments.All(a =>
                                    e.Grades.Any(g => g.AssessmentId == a.AssessmentId)))))
            .Select(e => e.CourseId)
            .Distinct()
            .CountAsync(cancellationToken);

    private async Task<int> GetPlannedCreditsAsync(
        IReadOnlyList<PlannedGradeDto> planned, CancellationToken cancellationToken)
    {
        if (planned.Count == 0)
        {
            return 0;
        }

        // Credits are earned by passing, whether or not the subject feeds the
        // GPA - so this deliberately does NOT filter on CountsTowardGpa.
        var passing = planned
            .Where(p => p.ExpectedScore >= AcademicRules.PassScore)
            .Select(p => p.CourseId)
            .Distinct()
            .ToList();

        if (passing.Count == 0)
        {
            return 0;
        }

        return await _db.Courses
            .AsNoTracking()
            .Where(c => passing.Contains(c.CourseId))
            .SumAsync(c => c.Credits, cancellationToken);
    }
}
