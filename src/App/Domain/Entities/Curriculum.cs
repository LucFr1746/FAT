namespace Domain.Entities;

/// <summary>
/// One row of a degree curriculum: major X takes course Y in term N.
/// This is the baseline that graduation progress is measured against.
/// </summary>
public class Curriculum
{
    public int CurriculumId { get; set; }
    public int MajorId { get; set; }
    public int CourseId { get; set; }

    /// <summary>
    /// Which kỳ of the programme this subject sits in. Points at Term.TermNo.
    ///
    /// ZERO-BASED, because FPT really does have a Kỳ 0 (OTP101, the orientation
    /// programme).
    ///
    /// The term lives HERE and not on Course on purpose: 16 subject codes in the
    /// FLM data sit in different kỳ depending on the major (ACC101 is Kỳ 1 for
    /// one programme and Kỳ 2 for another). A TermNo column on Course could only
    /// hold one of the two answers.
    /// </summary>
    public int TermNo { get; set; }

    /// <summary>Order of this subject WITHIN its term - what the reorder feature edits.</summary>
    public int DisplayOrder { get; set; }

    public bool IsMandatory { get; set; } = true;

    public Major? Major { get; set; }
    public Course? Course { get; set; }
    public Term? Term { get; set; }
}
