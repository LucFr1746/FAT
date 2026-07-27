using System.ComponentModel.DataAnnotations.Schema;
using FAT.Domain.Enums;

namespace FAT.Domain.Entities;

/// <summary>A student profile, paired one-to-one with a login account.</summary>
public class Student
{
    public int StudentId { get; set; }
    public int UserId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public int MajorId { get; set; }
    public string? CurrentSemester { get; set; }

    [NotMapped]
    public string? Campus { get; set; } = "Hồ Chí Minh";

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public AppUser? User { get; set; }
    public Major? Major { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<AcademicPlan> AcademicPlans { get; set; } = new List<AcademicPlan>();
}
