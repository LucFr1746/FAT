namespace SAT.Domain.Entities;

/// <summary>
/// Một đầu điểm của môn học, ví dụ "Assignment" 20%, "Final Exam" 40%.
/// Tổng <see cref="Weight"/> của mọi đầu điểm trong cùng một môn phải bằng 1.
/// </summary>
public class Assessment
{
    public int AssessmentId { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Trọng số dạng phân số: 0.40 nghĩa là 40%.</summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Điểm sàn riêng của đầu điểm này. Dưới ngưỡng là TRƯỢT MÔN kể cả khi
    /// điểm tổng kết vẫn từ 5.0 trở lên (quy chế thi cuối kỳ phải đạt >= 4).
    /// Null nghĩa là đầu điểm này không có điểm sàn riêng.
    /// </summary>
    public decimal? MinScoreToPass { get; set; }

    public int DisplayOrder { get; set; }

    public Course? Course { get; set; }
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}
