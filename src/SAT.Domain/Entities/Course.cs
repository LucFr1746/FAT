namespace SAT.Domain.Entities;

/// <summary>Môn học trong danh mục.</summary>
public class Course
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Các môn mà môn NÀY yêu cầu phải học trước.</summary>
    public ICollection<Prerequisite> Prerequisites { get; set; } = new List<Prerequisite>();

    /// <summary>Các môn coi môn NÀY là điều kiện tiên quyết (chiều ngược lại).</summary>
    public ICollection<Prerequisite> RequiredFor { get; set; } = new List<Prerequisite>();

    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Curriculum> CurriculumItems { get; set; } = new List<Curriculum>();
}
