using FAT.Domain.Enums;

namespace FAT.Domain.Constants;

/// <summary>
/// Rules that turn a GPA plus a study history into a graduation classification.
///
/// Separate from <see cref="AcademicRules"/>, which answers "what grade is this
/// score". This file answers "what degree does this record earn", which is a
/// different question with different inputs - retakes matter here and nowhere
/// else.
///
/// EVERY threshold lives in this file. A view model that hard-codes "3 retakes"
/// or writes its own demotion ladder will disagree with the prediction screen
/// the first time either is edited.
/// </summary>
public static class GraduationRules
{
    /// <summary>
    /// Retaking this many DISTINCT subjects costs one classification rank.
    ///
    /// FPT's regulation is about repeated subjects, not repeated attempts:
    /// failing one subject three times is one retaken subject, not three.
    /// </summary>
    public const int RetakeDemotionThreshold = 3;

    /// <summary>
    /// Cap on how far the retake penalty can push a classification down.
    /// Without it a long transcript would eventually demote every student to
    /// NotQualified, which is not what the regulation says.
    /// </summary>
    public const int MaxDemotionSteps = 1;

    /// <summary>
    /// The classification ladder, BEST FIRST. Demotion is a step along this
    /// array, which keeps the order in one place instead of in a switch.
    /// </summary>
    private static readonly DegreeClassification[] Ladder =
    [
        DegreeClassification.Excellent,
        DegreeClassification.VeryGood,
        DegreeClassification.Good,
        DegreeClassification.FairGood,
        DegreeClassification.Average,
        DegreeClassification.NotQualified
    ];

    /// <summary>
    /// How many ranks a student loses for <paramref name="retakenSubjectCount"/>
    /// retaken subjects.
    ///
    /// Uses &gt;= so that exactly three retakes already costs a rank - the
    /// boundary the requirement names explicitly.
    /// </summary>
    public static int GetDemotionSteps(int retakenSubjectCount)
        => retakenSubjectCount >= RetakeDemotionThreshold ? MaxDemotionSteps : 0;

    /// <summary>
    /// Moves a classification <paramref name="steps"/> ranks down the ladder.
    /// Never falls off the end: the worst outcome is NotQualified.
    /// </summary>
    public static DegreeClassification Demote(DegreeClassification classification, int steps)
    {
        if (steps <= 0)
        {
            return classification;
        }

        var index = Array.IndexOf(Ladder, classification);
        if (index < 0)
        {
            return classification;
        }

        var demotedIndex = Math.Min(index + steps, Ladder.Length - 1);
        return Ladder[demotedIndex];
    }

    /// <summary>
    /// The full rule: classify the GPA, then apply the retake penalty.
    /// This is the ONLY entry point callers should use.
    /// </summary>
    public static DegreeClassification ClassifyWithRetakes(decimal gpa, int retakenSubjectCount)
    {
        var baseClassification = AcademicRules.ClassifyGpa(gpa);
        return Demote(baseClassification, GetDemotionSteps(retakenSubjectCount));
    }

    /// <summary>Explains a demotion in words, ready to show next to the predicted result.</summary>
    public static string? DescribeDemotion(int retakenSubjectCount)
        => GetDemotionSteps(retakenSubjectCount) > 0
            ? $"Xếp loại bị giảm 1 bậc do học lại {retakenSubjectCount} môn (từ {RetakeDemotionThreshold} môn trở lên)."
            : null;
}
