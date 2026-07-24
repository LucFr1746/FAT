using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// GPA forecasting, including the effect retakes have on the final
/// classification.
///
/// The thresholds live in <c>Domain.Constants.GraduationRules</c>, never
/// here and never in a view model: the progress screen and this one must always
/// agree on what a given record earns.
/// </summary>
public interface IGpaPredictionService
{
    /// <summary>
    /// Projects the GPA assuming the given hypothetical scores are achieved,
    /// then applies the retake penalty.
    ///
    /// CROSS-CHECK worth keeping: feeding in nothing must reproduce the
    /// student's real current GPA exactly. That is the cheapest way to catch
    /// this formula drifting away from IGpaService.
    /// </summary>
    Task<GpaPredictionDto> PredictAsync(
        int studentId,
        IEnumerable<PlannedGradeDto>? plannedGrades = null,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the forecast so the student can see how it moved over time.</summary>
    Task<int> SaveSnapshotAsync(
        int studentId, GpaPredictionDto prediction, string? note = null,
        CancellationToken cancellationToken = default);

    /// <summary>Saved forecasts, newest first.</summary>
    Task<IReadOnlyList<GpaPredictionDto>> GetHistoryAsync(
        int studentId, int take = 10, CancellationToken cancellationToken = default);
}
