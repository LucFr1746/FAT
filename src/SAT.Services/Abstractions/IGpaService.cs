using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Tính GPA và tín chỉ (chức năng GPA Calculator).
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 4.
///
/// ⚠️ Đây là service NHIỀU NGƯỜI PHỤ THUỘC NHẤT: Statistics của chính Member 4,
/// và Curriculum Progress của Member 3 đều gọi nó. Phải giao bản chạy được
/// SỚM NHẤT trong nhóm, nếu không Member 3 bị chặn.
///
/// Quy tắc bắt buộc, mọi cài đặt phải tuân theo:
///   - Chỉ tính môn có Status = Passed VÀ IsCounted = true.
///   - Có trọng số theo tín chỉ: SUM(FinalScore * Credits) / SUM(Credits).
///   - Môn Failed / Withdrawn / Studying KHÔNG vào cả tử số lẫn mẫu số.
///   - Học lại chỉ tính lần cuối (đó chính là ý nghĩa của IsCounted).
/// </summary>
public interface IGpaService
{
    /// <summary>GPA tích lũy. Null khi chưa đạt môn nào (KHÔNG trả về 0).</summary>
    Task<decimal?> GetCumulativeGpaAsync(int studentId, CancellationToken cancellationToken = default);

    Task<GpaSummaryDto> GetGpaSummaryAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemesterGpaDto>> GetGpaBySemesterAsync(int studentId, CancellationToken cancellationToken = default);

    Task<CreditSummaryDto> GetCreditSummaryAsync(int studentId, CancellationToken cancellationToken = default);
}
