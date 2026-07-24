namespace Services.Import;

/// <summary>
/// Decides whether a syllabus session is an ASSESSMENT rather than an ordinary
/// lesson.
///
/// The workbook already applies this judgement: the "LichKiemTra" sheet holds
/// 2,927 of the 13,354 sessions in sessions.csv. The CSV reader has to make the
/// same call, or importing the folder would produce a schedule tens of times
/// larger than importing the workbook - and the two sources are meant to be
/// interchangeable.
///
/// Keyword matching is a heuristic and will occasionally misjudge a session.
/// That is acceptable here: the schedule is editable in the admin screen, and
/// the alternative - importing all 13,354 lessons - buries the real checkpoints.
/// </summary>
public static class AssessmentSessionFilter
{
    /// <summary>
    /// Words that mark a session as graded, in the two languages the FLM
    /// syllabi mix freely.
    /// </summary>
    private static readonly string[] AssessmentKeywords =
    [
        // Vietnamese
        "kiểm tra", "thi ", "thi cuối", "thi giữa", "bài tập lớn", "báo cáo",
        "thuyết trình", "đồ án", "nộp bài", "chấm điểm", "bảo vệ",
        // English
        "exam", "test", "quiz", "assignment", "presentation", "project",
        "submission", "submit", "assessment", "midterm", "mid-term", "final",
        "practical exam", "progress test", "workshop", "lab test", "defense"
    ];

    /// <summary>Whether a session topic describes a graded event.</summary>
    public static bool IsAssessmentSession(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var lowered = topic.ToLowerInvariant();
        return AssessmentKeywords.Any(keyword => lowered.Contains(keyword, StringComparison.Ordinal));
    }
}
