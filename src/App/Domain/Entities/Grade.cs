namespace Domain.Entities;

/// <summary>The actual score recorded for one component of one enrollment.</summary>
public class Grade
{
    public int GradeId { get; set; }
    public int EnrollmentId { get; set; }
    public int AssessmentId { get; set; }

    /// <summary>Score on the 10-point scale.</summary>
    public decimal Score { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Enrollment? Enrollment { get; set; }
    public Assessment? Assessment { get; set; }
}
