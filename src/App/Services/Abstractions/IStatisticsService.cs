using Services.Dtos;

namespace Services.Abstractions;

/// <summary>Aggregated, non-persisted academic performance statistics.</summary>
public interface IStatisticsService
{
    Task<AcademicStatisticsDto> GetStatisticsAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}
