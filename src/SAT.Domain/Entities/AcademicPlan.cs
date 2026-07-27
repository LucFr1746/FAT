namespace SAT.Domain.Entities;

/// <summary>Kế hoạch học tập cho các kỳ sắp tới của một sinh viên.</summary>
public class AcademicPlan
{
    public int PlanId { get; set; }
    public int StudentId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Student? Student { get; set; }
    public ICollection<AcademicPlanItem> Items { get; set; } = new List<AcademicPlanItem>();
}
