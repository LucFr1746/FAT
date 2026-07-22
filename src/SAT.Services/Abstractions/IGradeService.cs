using SAT.Domain.Entities;
using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Đăng ký môn, nhập điểm, chốt điểm tổng kết.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 4.
/// Phục vụ View Grades, Manage Grades, Transcript.
/// </summary>
public interface IGradeService
{
    Task<TranscriptDto> GetTranscriptAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Các đầu điểm và điểm hiện tại của một lần học môn.</summary>
    Task<IReadOnlyList<Grade>> GetGradesAsync(int enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi điểm cho một đầu điểm (thêm mới hoặc cập nhật), rồi TỰ ĐỘNG tính
    /// lại điểm tổng kết của môn đó.
    ///
    /// Gộp hai việc vào một lời gọi là cố ý: nếu tách ra, chỉ cần một chỗ quên
    /// gọi tính lại là bảng điểm và Dashboard hiển thị hai con số khác nhau.
    /// </summary>
    Task UpsertGradeAsync(int enrollmentId, int assessmentId, decimal score, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tính lại FinalScore, LetterGrade, GradePoint, Status cho một lần học môn.
    ///
    /// PHẢI cho ra kết quả GIỐNG HỆT phần tính điểm trong db/03_seed_demo.sql:
    ///   FinalScore = ROUND(SUM(Score * Weight), 1)
    ///   Đạt        = FinalScore >= 5.0 VÀ không đầu điểm nào dưới MinScoreToPass
    /// </summary>
    Task RecalculateFinalScoreAsync(int enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký môn. Kiểm tra tiên quyết và chặn trùng trước khi ghi.
    /// Trả về EnrollmentId mới.
    /// </summary>
    Task<int> EnrollAsync(int studentId, int courseId, int semesterId, CancellationToken cancellationToken = default);

    Task WithdrawAsync(int enrollmentId, CancellationToken cancellationToken = default);
}
