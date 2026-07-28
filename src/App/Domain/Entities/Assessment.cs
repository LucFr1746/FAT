namespace Domain.Entities;

/// <summary>
/// A grade component of a course, for example "Assignment" at 20% or
/// "Final Exam" at 40%. The <see cref="Weight"/> values of all components
/// belonging to one course must add up to exactly 1.
/// </summary>
public class Assessment
{
    public int AssessmentId { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Weight expressed as a fraction: 0.40 means 40%.</summary>
    public decimal Weight { get; set; }

    /// <summary>Number of parts/assignments in this assessment category (default 1).</summary>
    public int PartCount { get; set; } = 1;

    /// <summary>
    /// Minimum score required on this component alone. Falling below it FAILS
    /// the course even when the weighted total is 5.0 or higher (the usual
    /// rule that the final exam must reach at least 4).
    /// Null means this component has no individual minimum.
    /// </summary>
    public decimal? MinScoreToPass { get; set; }

    public int DisplayOrder { get; set; }

    public Course? Course { get; set; }
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}
