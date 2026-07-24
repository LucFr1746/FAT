using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// GPA simulation with hypothetical scores.
///
/// OUT OF SCOPE - NOT ASSIGNED TO ANYONE. See the note on IAcademicPlanService.
/// If someone does want a stretch feature, this is the one that demos best, and
/// it only reads IGpaService without touching anyone else's data.
/// </summary>
public interface IWhatIfService
{
    /// <summary>
    /// Projects the GPA assuming the given hypothetical scores are achieved.
    ///
    /// CROSS-CHECK: feeding in exactly the courses and scores the student
    /// ALREADY has must reproduce the real GPA. That is the cheapest way to
    /// catch a formula drifting apart from IGpaService.
    /// </summary>
    Task<WhatIfResultDto> SimulateAsync(int studentId, IEnumerable<PlannedGradeDto> plannedGrades, CancellationToken cancellationToken = default);

    /// <summary>
    /// The inverse question: what average score over the remaining
    /// <paramref name="remainingCredits"/> credits is needed to reach a target GPA.
    ///
    /// Returns null when the target is UNREACHABLE (the required score would
    /// exceed 10). Null here means "impossible", not "error".
    /// </summary>
    Task<decimal?> GetRequiredAverageScoreAsync(int studentId, decimal targetGpa, int remainingCredits, CancellationToken cancellationToken = default);
}
