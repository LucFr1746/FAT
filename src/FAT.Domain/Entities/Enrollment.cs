using FAT.Domain.Enums;

namespace FAT.Domain.Entities;

/// <summary>
/// One attempt by a student at a course in a given semester, with its outcome.
/// This is the central table of the application: GPA, credit totals and
/// graduation progress are all derived from it.
/// </summary>
public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int SemesterId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Studying;

    /// <summary>
    /// Final score on the 10-point scale. Null while the course is in progress.
    ///
    /// decimal, not double: accumulating binary rounding error across dozens of
    /// courses is enough to shift the GPA in its second decimal place.
    /// </summary>
    public decimal? FinalScore { get; set; }

    public string? LetterGrade { get; set; }
    public decimal? GradePoint { get; set; }

    /// <summary>
    /// Whether this attempt counts toward the GPA.
    ///
    /// When a course is retaken, only the LATEST attempt has IsCounted = true;
    /// earlier attempts stay in the transcript for history but are excluded
    /// from the GPA. Ignoring this flag is the classic bug that produces a
    /// suspiciously high GPA.
    /// </summary>
    public bool IsCounted { get; set; } = true;

    /// <summary>Which attempt this is (1 = first time taking the course).</summary>
    public int AttemptNo { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Student? Student { get; set; }
    public Course? Course { get; set; }
    public Semester? Semester { get; set; }
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}
