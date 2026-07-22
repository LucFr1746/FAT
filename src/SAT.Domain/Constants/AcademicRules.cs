using SAT.Domain.Enums;

namespace SAT.Domain.Constants;

/// <summary>
/// NGUỒN SỰ THẬT DUY NHẤT cho mọi quy tắc tính điểm của ứng dụng.
///
/// Ba module khác nhau phụ thuộc vào file này (Điểm/GPA của TV3, Analytics của
/// TV4, Tốt nghiệp/What-if của TV5). Nếu mỗi người tự viết ngưỡng riêng thì con
/// số trên Dashboard sẽ lệch với Bảng điểm, và lúc đó không còn thời gian sửa.
///
/// Muốn đổi ngưỡng: sửa ĐÚNG ở đây, đừng copy hằng số sang ViewModel.
/// </summary>
public static class AcademicRules
{
    /// <summary>Điểm tổng kết tối thiểu để đạt môn (thang 10).</summary>
    public const decimal PassScore = 5.0m;

    /// <summary>Điểm tổng kết được làm tròn tới số chữ số thập phân này.</summary>
    public const int FinalScoreDecimals = 1;

    /// <summary>GPA hiển thị tới số chữ số thập phân này.</summary>
    public const int GpaDecimals = 2;

    /// <summary>
    /// Trần tín chỉ được đăng ký trong một kỳ. Dùng để validate Academic Planner.
    /// </summary>
    public const int MaxCreditsPerSemester = 20;

    /// <summary>
    /// GPA của kỳ dưới ngưỡng này thì sinh viên bị cảnh báo học vụ.
    /// </summary>
    public const decimal AcademicWarningGpaThreshold = 5.0m;

    /// <summary>
    /// Trượt từ ngần này môn trở lên trong một kỳ cũng bị cảnh báo học vụ.
    /// </summary>
    public const int AcademicWarningFailedCourseCount = 2;

    /// <summary>Ngưỡng GPA cho từng mức xếp loại, sắp xếp từ CAO xuống THẤP.</summary>
    private static readonly (decimal MinGpa, DegreeClassification Classification)[] ClassificationThresholds =
    [
        (9.0m, DegreeClassification.Excellent),
        (8.0m, DegreeClassification.VeryGood),
        (7.0m, DegreeClassification.Good),
        (6.5m, DegreeClassification.FairGood),
        (5.0m, DegreeClassification.Average)
    ];

    /// <summary>
    /// Quy GPA (thang 10) ra xếp loại tốt nghiệp.
    /// So sánh dùng &gt;= nên đúng 8.0 là Giỏi chứ không phải Khá - đây là chỗ
    /// rất dễ viết nhầm thành &gt; và làm sai ngay tại các mốc tròn.
    /// </summary>
    public static DegreeClassification ClassifyGpa(decimal gpa)
    {
        foreach (var (minGpa, classification) in ClassificationThresholds)
        {
            if (gpa >= minGpa)
            {
                return classification;
            }
        }

        return DegreeClassification.NotQualified;
    }

    /// <summary>Tên tiếng Việt của xếp loại, dùng trực tiếp trên UI.</summary>
    public static string GetClassificationName(DegreeClassification classification) => classification switch
    {
        DegreeClassification.Excellent => "Xuất sắc",
        DegreeClassification.VeryGood => "Giỏi",
        DegreeClassification.Good => "Khá",
        DegreeClassification.FairGood => "Trung bình khá",
        DegreeClassification.Average => "Trung bình",
        _ => "Chưa đạt"
    };

    /// <summary>Làm tròn điểm tổng kết theo đúng quy ước của hệ thống.</summary>
    public static decimal RoundFinalScore(decimal score)
        => Math.Round(score, FinalScoreDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Làm tròn GPA theo đúng quy ước của hệ thống.</summary>
    public static decimal RoundGpa(decimal gpa)
        => Math.Round(gpa, GpaDecimals, MidpointRounding.AwayFromZero);
}
