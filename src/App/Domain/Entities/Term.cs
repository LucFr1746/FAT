namespace Domain.Entities;

/// <summary>
/// A term of the STUDY PATH ("Kỳ 1" ... "Kỳ 9") - the position a subject holds
/// inside a curriculum.
///
/// DO NOT CONFUSE THIS WITH <see cref="Semester"/>. They answer different
/// questions and both are needed:
///   - Term     = "which kỳ of the programme does this subject belong to"
///                -> referenced by Curriculum.TermNo, same for every cohort.
///   - Semester = "which calendar term did the student actually sit it in"
///                (FA25, SP26 - has dates, exactly one IsCurrent)
///                -> referenced by Enrollment, and the basis of GPA history.
///
/// Merging the two would force every Kỳ to carry a made-up StartDate/EndDate
/// and would make IsCurrent ambiguous.
/// </summary>
public class Term
{
    public int TermId { get; set; }

    /// <summary>
    /// The kỳ number. UNIQUE, and the column Curriculum.TermNo points at.
    ///
    /// Starts at ZERO, not one: FPT's real curriculum puts OTP101 (Orientation
    /// and General Training) in Kỳ 0.
    /// </summary>
    public int TermNo { get; set; }

    public string TermName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
}
