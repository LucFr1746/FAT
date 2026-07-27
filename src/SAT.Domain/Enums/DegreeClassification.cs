namespace SAT.Domain.Enums;

/// <summary>
/// Xếp loại tốt nghiệp theo GPA thang 10.
/// Ngưỡng cụ thể nằm ở <see cref="Constants.AcademicRules"/> - KHÔNG rải rác
/// trong ViewModel, vì Dashboard và màn Tốt nghiệp phải cho ra cùng một kết quả.
/// </summary>
public enum DegreeClassification
{
    /// <summary>Không đạt (GPA &lt; 5.0).</summary>
    NotQualified = 0,

    /// <summary>Trung bình (5.0 - 6.4).</summary>
    Average = 1,

    /// <summary>Trung bình khá (6.5 - 6.9).</summary>
    FairGood = 2,

    /// <summary>Khá (7.0 - 7.9).</summary>
    Good = 3,

    /// <summary>Giỏi (8.0 - 8.9).</summary>
    VeryGood = 4,

    /// <summary>Xuất sắc (9.0 - 10.0).</summary>
    Excellent = 5
}
