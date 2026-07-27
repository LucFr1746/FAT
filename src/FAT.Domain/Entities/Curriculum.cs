namespace FAT.Domain.Entities;

/// <summary>
/// One row of a degree curriculum: major X takes course Y in term N.
/// This is the baseline that graduation progress is measured against.
/// </summary>
public class Curriculum
{
    public int CurriculumId { get; set; }
    public int MajorId { get; set; }
    public int CourseId { get; set; }

    /// <summary>Position in the standard study path (1-based).</summary>
    public int TermNo { get; set; }

    public bool IsMandatory { get; set; } = true;

    public Major? Major { get; set; }
    public Course? Course { get; set; }
}
