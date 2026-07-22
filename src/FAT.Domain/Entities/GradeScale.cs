namespace FAT.Domain.Entities;

/// <summary>
/// One band of the table that converts a numeric score into a letter grade
/// and a 4-point grade point.
///
/// The band is HALF-OPEN: <c>MinScore &lt;= Score &lt; MaxScore</c>.
/// That guarantees the bands neither overlap nor leave a gap (such as 8.45).
/// </summary>
public class GradeScale
{
    public int GradeScaleId { get; set; }

    /// <summary>Lower bound, INCLUSIVE.</summary>
    public decimal MinScore { get; set; }

    /// <summary>Upper bound, EXCLUSIVE.</summary>
    public decimal MaxScore { get; set; }

    public string LetterGrade { get; set; } = string.Empty;
    public decimal GradePoint { get; set; }
    public string? Description { get; set; }

    /// <summary>Whether the given score falls into this band.</summary>
    public bool Contains(decimal score) => score >= MinScore && score < MaxScore;
}
