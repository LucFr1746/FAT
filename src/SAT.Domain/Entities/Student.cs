using SAT.Domain.Enums;

namespace SAT.Domain.Entities;

/// <summary>Hồ sơ sinh viên, gắn 1-1 với một tài khoản đăng nhập.</summary>
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
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public AppUser? User { get; set; }
    public Major? Major { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<AcademicPlan> AcademicPlans { get; set; } = new List<AcademicPlan>();
}
