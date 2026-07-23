namespace FAT.Domain.Enums;

/// <summary>
/// Graduation classification derived from the cumulative GPA (10-point scale).
/// The actual thresholds live in <see cref="Constants.AcademicRules"/> - never
/// scattered across view models, because the dashboard and the progress screen
/// must always agree on the answer.
/// </summary>
public enum DegreeClassification
{
    /// <summary>Not qualified (GPA below 5.0).</summary>
    NotQualified = 0,

    /// <summary>Average (5.0 - 6.4).</summary>
    Average = 1,

    /// <summary>Fairly good (6.5 - 6.9).</summary>
    FairGood = 2,

    /// <summary>Good (7.0 - 7.9).</summary>
    Good = 3,

    /// <summary>Very good (8.0 - 8.9).</summary>
    VeryGood = 4,

    /// <summary>Excellent (9.0 - 10.0).</summary>
    Excellent = 5
}
