using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Mô phỏng GPA với điểm giả định.
///
/// ⚠️ NGOÀI PHẠM VI - CHƯA GÁN CHO AI. Xem ghi chú ở IAcademicPlanService.
/// Nếu có người muốn làm thêm thì đây là tính năng gây ấn tượng nhất khi demo,
/// và nó chỉ cần đọc IGpaService chứ không đụng vào dữ liệu của ai.
/// </summary>
public interface IWhatIfService
{
    /// <summary>
    /// Tính GPA dự phóng nếu sinh viên đạt các điểm giả định đã cho.
    ///
    /// KIỂM TRA CHÉO: truyền vào đúng những môn và điểm mà sinh viên ĐÃ có thì
    /// kết quả phải bằng GPA thật. Đây là cách rẻ nhất để bắt lỗi lệch công
    /// thức giữa WhatIfService và GpaService (docs/plan §9).
    /// </summary>
    Task<WhatIfResultDto> SimulateAsync(int studentId, IEnumerable<PlannedGradeDto> plannedGrades, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bài toán ngược: cần điểm trung bình bao nhiêu cho <paramref name="remainingCredits"/>
    /// tín chỉ còn lại để đạt GPA mục tiêu.
    ///
    /// Trả về null khi mục tiêu KHÔNG THỂ đạt được (điểm cần vượt quá 10) -
    /// null ở đây có nghĩa là "bất khả thi", không phải "lỗi".
    /// </summary>
    Task<decimal?> GetRequiredAverageScoreAsync(int studentId, decimal targetGpa, int remainingCredits, CancellationToken cancellationToken = default);
}
