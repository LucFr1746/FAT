namespace SAT.Domain.Entities;

/// <summary>
/// Một dòng trong bảng quy đổi điểm số sang điểm chữ và thang 4.
///
/// Khoảng là NỬA MỞ: <c>MinScore &lt;= Score &lt; MaxScore</c>.
/// Nhờ vậy các mức không chồng lấn và không để lọt giá trị nào (ví dụ 8.45).
/// </summary>
public class GradeScale
{
    public int GradeScaleId { get; set; }

    /// <summary>Cận dưới, BAO GỒM giá trị này.</summary>
    public decimal MinScore { get; set; }

    /// <summary>Cận trên, KHÔNG bao gồm giá trị này.</summary>
    public decimal MaxScore { get; set; }

    public string LetterGrade { get; set; } = string.Empty;
    public decimal GradePoint { get; set; }
    public string? Description { get; set; }

    /// <summary>Điểm đã cho có rơi vào mức này không.</summary>
    public bool Contains(decimal score) => score >= MinScore && score < MaxScore;
}
