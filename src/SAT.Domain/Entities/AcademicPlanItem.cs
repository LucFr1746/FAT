namespace SAT.Domain.Entities;

/// <summary>Một môn được xếp vào kế hoạch học tập.</summary>
public class AcademicPlanItem
{
    public int PlanItemId { get; set; }
    public int PlanId { get; set; }
    public int CourseId { get; set; }

    /// <summary>Kỳ cụ thể, nếu sinh viên đã chọn được kỳ.</summary>
    public int? SemesterId { get; set; }

    /// <summary>Hoặc chỉ là "kỳ thứ N" khi chưa gắn vào Semester cụ thể.</summary>
    public int? TargetTermNo { get; set; }

    /// <summary>Điểm kỳ vọng - đầu vào của tính năng What-if GPA.</summary>
    public decimal? ExpectedScore { get; set; }

    public int DisplayOrder { get; set; }

    public AcademicPlan? Plan { get; set; }
    public Course? Course { get; set; }
    public Semester? Semester { get; set; }
}
