using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Lập kế hoạch học tập cho các kỳ sắp tới.
///
/// ⚠️ NGOÀI PHẠM VI - CHƯA GÁN CHO AI.
/// Bảng phân công 5 thành viên không có chức năng này. Interface được giữ lại
/// vì bảng AcademicPlan/AcademicPlanItem đã có sẵn trong schema, nhưng KHÔNG
/// AI phải cài đặt nó để hoàn thành phần việc của mình. Chỉ làm khi đã xong
/// hết 5 chức năng được giao và còn thời gian.
/// </summary>
public interface IAcademicPlanService
{
    Task<IReadOnlyList<AcademicPlanDto>> GetPlansAsync(int studentId, CancellationToken cancellationToken = default);
    Task<AcademicPlanDto?> GetPlanAsync(int planId, CancellationToken cancellationToken = default);

    Task<int> CreatePlanAsync(int studentId, string planName, string? note, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(int planId, CancellationToken cancellationToken = default);

    Task<int> AddItemAsync(int planId, int courseId, int? semesterId, int? targetTermNo, decimal? expectedScore, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(int planItemId, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(int planItemId, int? semesterId, int? targetTermNo, decimal? expectedScore, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra kế hoạch: điều kiện tiên quyết, trần tín chỉ mỗi kỳ
    /// (AcademicRules.MaxCreditsPerSemester), môn đã đạt bị xếp lại.
    ///
    /// Trả về danh sách lỗi/cảnh báo thay vì ném exception, vì kế hoạch không
    /// hợp lệ vẫn cần hiển thị được để sinh viên tự sửa.
    /// </summary>
    Task<PlanValidationResult> ValidatePlanAsync(int planId, CancellationToken cancellationToken = default);
}
