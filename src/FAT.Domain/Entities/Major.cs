namespace FAT.Domain.Entities;

/// <summary>A degree programme.</summary>
public class Major
{
    public int MajorId { get; set; }
    public string MajorCode { get; set; } = string.Empty;
    public string MajorName { get; set; } = string.Empty;

    /// <summary>
    /// Total credits required to graduate. This is the DENOMINATOR of the
    /// graduation progress percentage, so it must always equal the sum of the
    /// credits in this major's curriculum.
    /// </summary>
    public int RequiredCredits { get; set; }

    public int TotalTerms { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
}
