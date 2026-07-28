using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// GPA and credit calculation (the GPA Calculator feature).
/// FROZEN CONTRACT - owner: Member 4.
///
/// THIS IS THE MOST DEPENDED-ON SERVICE IN THE PROJECT: Member 4's own
/// Statistics screen and Member 3's Curriculum Progress both call it. Ship a
/// working version FIRST, before anything else, or Member 3 is blocked.
///
/// Rules every implementation must honour:
///   - Count completed rows with Status = Passed or Failed and IsCounted = true.
///   - Weight by credits: SUM(FinalScore * Credits) / SUM(Credits).
///   - Failed contributes with its real score and credits; Withdrawn, Studying
///     and Not Graded contribute to neither numerator nor denominator.
///   - A retaken course counts once, via its latest attempt - that is exactly
///     what IsCounted encodes.
/// </summary>
public interface IGpaService
{
    /// <summary>Cumulative GPA. Null when no completed result qualifies (NOT zero).</summary>
    Task<decimal?> GetCumulativeGpaAsync(int studentId, CancellationToken cancellationToken = default);

    Task<GpaSummaryDto> GetGpaSummaryAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemesterGpaDto>> GetGpaBySemesterAsync(int studentId, CancellationToken cancellationToken = default);

    Task<CreditSummaryDto> GetCreditSummaryAsync(int studentId, CancellationToken cancellationToken = default);
}
