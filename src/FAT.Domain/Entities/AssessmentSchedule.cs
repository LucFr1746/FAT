namespace FAT.Domain.Entities;

/// <summary>
/// One planned checkpoint on a subject's timeline: "in session 15 there is a
/// progress test". This is the syllabus schedule, shared by every student
/// taking the subject - it is NOT a per-student calendar.
/// </summary>
public class AssessmentSchedule
{
    public int AssessmentScheduleId { get; set; }
    public int CourseId { get; set; }

    /// <summary>Session number within the subject (1-based), as published by FLM.</summary>
    public int SessionNo { get; set; }

    /// <summary>
    /// Teaching week, derived from <see cref="SessionNo"/> at import time.
    /// Nullable because a subject that does not run on a weekly rhythm
    /// (block courses, OJT) has no meaningful week number.
    /// </summary>
    public int? WeekNo { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// FLM publishes session numbers but NO dates, so this imports as null and
    /// an administrator fills it in once the semester calendar is known.
    /// </summary>
    public DateTime? ExpectedDate { get; set; }

    /// <summary>Offline | Online | ... - free text, exactly as FLM publishes it.</summary>
    public string? TeachingType { get; set; }

    /// <summary>
    /// Optional link to the grade component this checkpoint feeds.
    /// Null when the session is a plain lesson rather than a graded event.
    /// </summary>
    public int? AssessmentId { get; set; }

    public Course? Course { get; set; }
    public Assessment? Assessment { get; set; }
}
