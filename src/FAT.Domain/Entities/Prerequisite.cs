using FAT.Domain.Enums;

namespace FAT.Domain.Entities;

/// <summary>
/// A prerequisite edge: to take <see cref="CourseId"/> a student must first
/// pass <see cref="RequiredCourseId"/>.
/// </summary>
public class Prerequisite
{
    public int PrerequisiteId { get; set; }

    /// <summary>The course that carries the requirement.</summary>
    public int CourseId { get; set; }

    /// <summary>The course that must be completed first.</summary>
    public int RequiredCourseId { get; set; }

    public PrerequisiteType Type { get; set; } = PrerequisiteType.Prerequisite;

    /// <summary>
    /// Turns the flat list into AND-of-ORs, which real syllabi need:
    /// MKT205c requires "MKT101 or MKG101 or MMK101 or IBI101" while HCM202
    /// requires "MLN111, MLN122" - both of them.
    ///
    ///   GroupNo = 0  -> a standalone requirement, AND-ed with everything else.
    ///   GroupNo &gt; 0  -> one alternative; passing ANY row sharing that GroupNo
    ///                   satisfies the whole group.
    ///
    /// A course is unlocked when every group is satisfied.
    /// </summary>
    public int GroupNo { get; set; }

    public Course? Course { get; set; }
    public Course? RequiredCourse { get; set; }
}
