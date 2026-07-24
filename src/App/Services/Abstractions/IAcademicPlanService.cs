using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Study planning for upcoming semesters.
///
/// OUT OF SCOPE - NOT ASSIGNED TO ANYONE.
/// The five-member feature breakdown does not include this. The interface is
/// kept because the AcademicPlan and AcademicPlanItem tables already exist in
/// the schema, but NOBODY has to implement it to finish their own work. Pick it
/// up only after all five assigned features are done and there is time left.
/// </summary>
public interface IAcademicPlanService
{
    Task<IReadOnlyList<AcademicPlanDto>> GetPlansAsync(int studentId, CancellationToken cancellationToken = default);
    Task<AcademicPlanDto?> GetPlanAsync(int planId, CancellationToken cancellationToken = default);

    Task<int> CreatePlanAsync(int studentId, string planName, string? note, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(int planId, CancellationToken cancellationToken = default);

    Task<int> AddItemAsync(int planId, int courseId, int? semesterId, int? targetTermNo, decimal? expectedScore, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(int planItemId, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(int planItemId, int? semesterId, int? targetTermNo, decimal? expectedScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a plan: prerequisites, the per-semester credit ceiling
    /// (AcademicRules.MaxCreditsPerSemester), and already-passed courses that
    /// have been scheduled again.
    ///
    /// Returns errors and warnings rather than throwing, because an invalid
    /// plan still has to be displayed for the student to correct it.
    /// </summary>
    Task<PlanValidationResult> ValidatePlanAsync(int planId, CancellationToken cancellationToken = default);
}
