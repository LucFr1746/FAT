namespace FAT.Domain.Constants;

/// <summary>
/// Limits and tolerances for the catalog (Major, Term, Subject, grade
/// structure, curriculum).
///
/// These mirror the column widths and CHECK constraints in db/01_schema.sql.
/// They are duplicated in C# ON PURPOSE: a validation failure has to reach the
/// user as a sentence in the form, not as a SqlException 800 lines deep. When a
/// value changes here it must change in the schema too.
/// </summary>
public static class CatalogRules
{
    // ----- Field widths (must match db/01_schema.sql) -----
    public const int MajorCodeMaxLength = 20;
    public const int MajorNameMaxLength = 150;
    public const int DescriptionMaxLength = 500;

    public const int TermNameMaxLength = 50;

    public const int CourseCodeMaxLength = 20;
    public const int CourseNameMaxLength = 200;

    public const int SemesterCodeMaxLength = 10;
    public const int SemesterNameMaxLength = 50;

    public const int AssessmentNameMaxLength = 200;
    public const int MaterialTitleMaxLength = 500;
    public const int UrlMaxLength = 500;

    // ----- Numeric bounds (must match the CHECK constraints) -----

    /// <summary>Lowest legal kỳ. Zero, because OTP101 really is Kỳ 0.</summary>
    public const int MinTermNo = 0;

    /// <summary>Highest kỳ the UI offers. The longest FLM programme runs to Kỳ 9.</summary>
    public const int MaxTermNo = 12;

    public const int MinCredits = 0;
    public const int MaxCredits = 20;

    /// <summary>Weights of one subject's grade components must add up to this.</summary>
    public const decimal TotalAssessmentWeight = 1.0m;

    /// <summary>
    /// Slack allowed on that sum.
    ///
    /// Needed because thirds are unavoidable: a 33.3/33.3/33.4 split is a real
    /// FLM structure and stores as three 4-decimal values that do not add up to
    /// exactly 1. Demanding exact equality would reject valid data.
    /// </summary>
    public const decimal AssessmentWeightTolerance = 0.0005m;

    /// <summary>Decimal places kept on a weight - matches Assessment.Weight DECIMAL(5,4).</summary>
    public const int AssessmentWeightDecimals = 4;

    /// <summary>
    /// Sessions per teaching week, used to derive AssessmentSchedule.WeekNo from
    /// the session number that FLM publishes. FPT runs two slots a week.
    /// </summary>
    public const int DefaultSessionsPerWeek = 2;

    // ----- Paging -----
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    /// <summary>True when a set of weights is close enough to 1.00 to accept.</summary>
    public static bool IsWeightTotalValid(decimal totalWeight)
        => Math.Abs(totalWeight - TotalAssessmentWeight) <= AssessmentWeightTolerance;

    /// <summary>Teaching week that a 1-based session number falls in.</summary>
    public static int GetWeekNo(int sessionNo, int sessionsPerWeek = DefaultSessionsPerWeek)
    {
        if (sessionNo < 1 || sessionsPerWeek < 1)
        {
            return 1;
        }

        return ((sessionNo - 1) / sessionsPerWeek) + 1;
    }

    /// <summary>Display name of a kỳ, e.g. "Kỳ 3". Kỳ 0 is the orientation block.</summary>
    public static string GetTermName(int termNo)
        => termNo == 0 ? "Kỳ 0 (Định hướng)" : $"Kỳ {termNo}";
}
