using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

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

    /// <summary>
    /// Which kỳ the student is in right now - the term the curriculum screen
    /// opens on. Points at Term.TermNo.
    ///
    /// <see cref="CurrentSemester"/> is the older free-text version of the same
    /// fact ("Kỳ 5"). Both are kept in sync from one place
    /// (IStudentCurriculumService.SetCurrentTermAsync) so the Profile screen,
    /// which binds to the string, keeps working.
    /// </summary>
    public int? CurrentTermNo { get; set; }

    /// <summary>Display form of <see cref="CurrentTermNo"/>, e.g. "Kỳ 5".</summary>
    public string? CurrentSemester { get; set; }

    [NotMapped]
    public string? Campus { get; set; } = "Hồ Chí Minh";

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public AppUser? User { get; set; }
    public Major? Major { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<AcademicPlan> AcademicPlans { get; set; } = new List<AcademicPlan>();
}
