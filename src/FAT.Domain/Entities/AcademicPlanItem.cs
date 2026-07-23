namespace FAT.Domain.Entities;

/// <summary>A course placed into a study plan.</summary>
public class AcademicPlanItem
{
    public int PlanItemId { get; set; }
    public int PlanId { get; set; }
    public int CourseId { get; set; }

    /// <summary>A concrete semester, once the student has picked one.</summary>
    public int? SemesterId { get; set; }

    /// <summary>Or simply "term N" when no concrete semester is chosen yet.</summary>
    public int? TargetTermNo { get; set; }

    /// <summary>Expected score - the input to the what-if GPA feature.</summary>
    public decimal? ExpectedScore { get; set; }

    public int DisplayOrder { get; set; }

    public AcademicPlan? Plan { get; set; }
    public Course? Course { get; set; }
    public Semester? Semester { get; set; }
}
