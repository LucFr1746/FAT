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

    public Course? Course { get; set; }
    public Course? RequiredCourse { get; set; }
}
