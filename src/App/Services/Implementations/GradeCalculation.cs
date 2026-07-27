using Domain.Constants;

namespace Services.Implementations;

/// <summary>
/// Pure grade calculations shared by grade settlement and unit tests.
/// </summary>
public static class GradeCalculation
{
    public static void ValidateScore(decimal score)
    {
        if (score is < 0m or > 10m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                score,
                "Score must be between 0 and 10.");
        }
    }

    /// <summary>
    /// Returns null until every assessment has a score. An empty structure also
    /// has no final score.
    /// </summary>
    public static decimal? CalculateFinalScore(
        IEnumerable<(decimal Weight, decimal? Score)> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var values = components.ToList();
        if (values.Count == 0 || values.Any(component => !component.Score.HasValue))
        {
            return null;
        }

        foreach (var component in values)
        {
            if (component.Weight is <= 0m or > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(components),
                    "Assessment weight must be greater than 0 and no greater than 1.");
            }

            ValidateScore(component.Score!.Value);
        }

        var weightedTotal = values.Sum(component => component.Score!.Value * component.Weight);
        return AcademicRules.RoundFinalScore(weightedTotal);
    }
}
