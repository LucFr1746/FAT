namespace SAT.Domain.Entities;

/// <summary>Điểm thực tế của một đầu điểm trong một lần học môn.</summary>
public class Grade
{
    public int GradeId { get; set; }
    public int EnrollmentId { get; set; }
    public int AssessmentId { get; set; }

    /// <summary>Điểm thang 10.</summary>
    public decimal Score { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Enrollment? Enrollment { get; set; }
    public Assessment? Assessment { get; set; }
}
