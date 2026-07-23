using SAT.Domain.Enums;

namespace SAT.Domain.Entities;

/// <summary>
/// Một lần sinh viên học một môn trong một kỳ, kèm kết quả cuối cùng.
/// Đây là bảng trung tâm của toàn ứng dụng: GPA, tín chỉ, tiến độ tốt nghiệp
/// đều tính ra từ đây.
/// </summary>
public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int SemesterId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Studying;

    /// <summary>
    /// Điểm tổng kết thang 10. Null khi môn còn đang học.
    /// Kiểu decimal (không phải double) để cộng dồn qua hàng chục môn không
    /// sinh sai số nhị phân làm lệch GPA.
    /// </summary>
    public decimal? FinalScore { get; set; }

    public string? LetterGrade { get; set; }
    public decimal? GradePoint { get; set; }

    /// <summary>
    /// Lần học này có được tính vào GPA không.
    /// Khi học lại một môn, chỉ lần MỚI NHẤT có IsCounted = true; các lần
    /// trước vẫn nằm trong bảng điểm để xem lịch sử nhưng bị loại khỏi GPA.
    /// Bỏ qua cờ này là nguyên nhân kinh điển làm GPA cao bất thường.
    /// </summary>
    public bool IsCounted { get; set; } = true;

    /// <summary>Lần học thứ mấy của môn này (1 = học lần đầu).</summary>
    public int AttemptNo { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Student? Student { get; set; }
    public Course? Course { get; set; }
    public Semester? Semester { get; set; }
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}
