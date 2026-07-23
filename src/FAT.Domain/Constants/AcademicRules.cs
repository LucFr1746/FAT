using FAT.Domain.Enums;

namespace FAT.Domain.Constants;

/// <summary>
/// THE single source of truth for every grading rule in the application.
///
/// Three separate modules depend on this file (Grade/GPA for Member 4,
/// Curriculum Progress for Member 3, Statistics for Member 4). If each of them
/// hard-coded its own thresholds, the number on the dashboard would drift away
/// from the number on the transcript - and there would be no time left to fix it.
///
/// To change a threshold: edit it HERE. Never copy a constant into a view model.
/// </summary>
public static class AcademicRules
{
    /// <summary>Minimum final score required to pass a course (10-point scale).</summary>
    public const decimal PassScore = 5.0m;

    /// <summary>Final scores are rounded to this many decimal places.</summary>
    public const int FinalScoreDecimals = 1;

    /// <summary>GPA values are displayed with this many decimal places.</summary>
    public const int GpaDecimals = 2;

    /// <summary>
    /// Maximum credits a student may register for in one semester.
    /// Used to validate academic plans.
    /// </summary>
    public const int MaxCreditsPerSemester = 20;

    /// <summary>A semester GPA below this threshold triggers an academic warning.</summary>
    public const decimal AcademicWarningGpaThreshold = 5.0m;

    /// <summary>Failing this many courses in one semester also triggers a warning.</summary>
    public const int AcademicWarningFailedCourseCount = 2;

    /// <summary>GPA thresholds per classification, ordered from HIGHEST to LOWEST.</summary>
    private static readonly (decimal MinGpa, DegreeClassification Classification)[] ClassificationThresholds =
    [
        (9.0m, DegreeClassification.Excellent),
        (8.0m, DegreeClassification.VeryGood),
        (7.0m, DegreeClassification.Good),
        (6.5m, DegreeClassification.FairGood),
        (5.0m, DegreeClassification.Average)
    ];

    /// <summary>
    /// Maps a GPA (10-point scale) to its graduation classification.
    ///
    /// The comparison uses &gt;= so that exactly 8.0 is VeryGood rather than Good.
    /// Writing &gt; here is an easy mistake that silently produces wrong results
    /// at every round threshold - which is exactly where reviewers look first.
    /// </summary>
    public static DegreeClassification ClassifyGpa(decimal gpa)
    {
        foreach (var (minGpa, classification) in ClassificationThresholds)
        {
            if (gpa >= minGpa)
            {
                return classification;
            }
        }

        return DegreeClassification.NotQualified;
    }

    /// <summary>Display name of a classification, ready to bind to the UI.</summary>
    public static string GetClassificationName(DegreeClassification classification) => classification switch
    {
        DegreeClassification.Excellent => "Excellent",
        DegreeClassification.VeryGood => "Very Good",
        DegreeClassification.Good => "Good",
        DegreeClassification.FairGood => "Fairly Good",
        DegreeClassification.Average => "Average",
        _ => "Not Qualified"
    };

    /// <summary>Rounds a final score using the convention agreed for this system.</summary>
    public static decimal RoundFinalScore(decimal score)
        => Math.Round(score, FinalScoreDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Rounds a GPA using the convention agreed for this system.</summary>
    public static decimal RoundGpa(decimal gpa)
        => Math.Round(gpa, GpaDecimals, MidpointRounding.AwayFromZero);
}
