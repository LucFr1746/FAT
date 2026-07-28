using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A saved snapshot of a GPA forecast.
///
/// READ-ONLY HISTORY. Nothing here ever feeds back into the real GPA: the only
/// source of truth for that is a completed Enrollment (Passed or Failed) with
/// IsCounted = true. This table only lets a student review how their projection
/// moved over time.
/// </summary>
public class GradePrediction
{
    public int GradePredictionId { get; set; }
    public int StudentId { get; set; }

    /// <summary>Cumulative GPA at the moment of the forecast. Null when nothing was passed yet.</summary>
    public decimal? CurrentGpa { get; set; }

    public decimal PredictedGpa { get; set; }

    /// <summary>Distinct current subjects with an official final score of zero.</summary>
    public int RetakeCount { get; set; }

    /// <summary>Classification implied by <see cref="PredictedGpa"/> alone.</summary>
    public DegreeClassification BaseClassification { get; set; }

    /// <summary>Classification after the retake penalty in GraduationRules.</summary>
    public DegreeClassification AdjustedClassification { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Student? Student { get; set; }
}
