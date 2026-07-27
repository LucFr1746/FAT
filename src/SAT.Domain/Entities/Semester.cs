namespace SAT.Domain.Entities;

/// <summary>Học kỳ.</summary>
public class Semester
{
    public int SemesterId { get; set; }

    /// <summary>Mã ngắn hiển thị trên UI, ví dụ "FA25".</summary>
    public string SemesterCode { get; set; } = string.Empty;

    public string SemesterName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Thứ tự thời gian thật. LUÔN sắp xếp theo trường này.
    /// Sắp theo <see cref="SemesterCode"/> là SAI: "FA25" đứng trước "SP26"
    /// theo alphabet nhưng FA25 lại diễn ra trước SP26 về mặt thời gian, nên
    /// chuỗi GPA theo kỳ sẽ bị vẽ sai thứ tự.
    /// </summary>
    public int DisplayOrder { get; set; }

    public bool IsCurrent { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
