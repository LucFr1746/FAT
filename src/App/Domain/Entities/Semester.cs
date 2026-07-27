namespace Domain.Entities;

/// <summary>An academic term.</summary>
public class Semester
{
    public int SemesterId { get; set; }

    /// <summary>Short code shown in the UI, for example "FA25".</summary>
    public string SemesterCode { get; set; } = string.Empty;

    public string SemesterName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// True chronological order. ALWAYS sort by this column.
    ///
    /// Sorting by <see cref="SemesterCode"/> is WRONG: "FA25" sorts before
    /// "SP26" alphabetically, yet FA25 happens earlier in time - so a GPA
    /// trend chart ordered by code comes out in the wrong sequence.
    /// </summary>
    public int DisplayOrder { get; set; }

    public bool IsCurrent { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
