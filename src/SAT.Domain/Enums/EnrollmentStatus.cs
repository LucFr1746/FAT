namespace SAT.Domain.Enums;

/// <summary>
/// Trạng thái của một lần sinh viên đăng ký học một môn.
/// Lưu xuống DB dưới dạng CHUỖI (xem EnrollmentConfiguration) để đọc thẳng
/// bằng SSMS vẫn hiểu được, thay vì thấy số 0/1/2 vô nghĩa.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Đang học, chưa chốt điểm tổng kết.</summary>
    Studying = 0,

    /// <summary>Đã đạt. Chỉ trạng thái này mới được tính vào GPA.</summary>
    Passed = 1,

    /// <summary>Trượt: điểm tổng kết dưới 5.0 hoặc có đầu điểm dưới điểm sàn.</summary>
    Failed = 2,

    /// <summary>Rút môn giữa kỳ. Không tính vào GPA và không tính tín chỉ.</summary>
    Withdrawn = 3
}
