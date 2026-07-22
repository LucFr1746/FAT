using SAT.Domain.Enums;

namespace SAT.Domain.Entities;

/// <summary>
/// Ràng buộc tiên quyết: muốn học <see cref="CourseId"/> thì phải đạt
/// <see cref="RequiredCourseId"/> trước.
/// </summary>
public class Prerequisite
{
    public int PrerequisiteId { get; set; }

    /// <summary>Môn có điều kiện.</summary>
    public int CourseId { get; set; }

    /// <summary>Môn phải học trước.</summary>
    public int RequiredCourseId { get; set; }

    public PrerequisiteType Type { get; set; } = PrerequisiteType.Prerequisite;

    public Course? Course { get; set; }
    public Course? RequiredCourse { get; set; }
}
