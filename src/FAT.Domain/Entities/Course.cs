namespace FAT.Domain.Entities;

/// <summary>A course in the catalog.</summary>
public class Course
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Courses that THIS course requires to be completed first.</summary>
    public ICollection<Prerequisite> Prerequisites { get; set; } = new List<Prerequisite>();

    /// <summary>Courses that list THIS course as their prerequisite (reverse direction).</summary>
    public ICollection<Prerequisite> RequiredFor { get; set; } = new List<Prerequisite>();

    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
}
