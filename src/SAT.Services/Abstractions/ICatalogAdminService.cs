using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Quản trị danh mục - PHẦN GHI. Chỉ tài khoản Admin được gọi.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 2.
///
/// Phụ trách đủ 5 chức năng: Manage Major, Manage Semester, Manage Subject,
/// Assign Subject to Major, Curriculum Management.
/// </summary>
public interface ICatalogAdminService
{
    // ----- Manage Major -----
    Task<int> CreateMajorAsync(MajorDto major, CancellationToken cancellationToken = default);
    Task UpdateMajorAsync(MajorDto major, CancellationToken cancellationToken = default);
    Task DeactivateMajorAsync(int majorId, CancellationToken cancellationToken = default);

    // ----- Manage Semester -----
    Task<int> CreateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default);
    Task UpdateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt kỳ hiện tại. Cài đặt phải BỎ cờ IsCurrent của kỳ cũ trong cùng một
    /// transaction: DB có ràng buộc ngầm là chỉ đúng một kỳ được IsCurrent = 1,
    /// và 02_seed_master.sql kiểm tra điều đó.
    /// </summary>
    Task SetCurrentSemesterAsync(int semesterId, CancellationToken cancellationToken = default);

    // ----- Manage Subject -----
    Task<int> CreateCourseAsync(CourseDto course, CancellationToken cancellationToken = default);
    Task UpdateCourseAsync(CourseDto course, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vô hiệu hóa môn (IsActive = false) thay vì xóa cứng.
    /// Xóa cứng sẽ kéo theo bảng điểm lịch sử của sinh viên đã học môn đó.
    /// </summary>
    Task DeactivateCourseAsync(int courseId, CancellationToken cancellationToken = default);

    // ----- Assign Subject to Major / Curriculum Management -----
    Task<int> AssignCourseToMajorAsync(int majorId, int courseId, int termNo, bool isMandatory, CancellationToken cancellationToken = default);
    Task RemoveCourseFromMajorAsync(int curriculumId, CancellationToken cancellationToken = default);
    Task UpdateCurriculumItemAsync(int curriculumId, int termNo, bool isMandatory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật Major.RequiredCredits cho khớp tổng tín chỉ hiện tại của khung
    /// chương trình. PHẢI gọi sau mỗi lần thêm/bớt môn khỏi khung: hai con số
    /// lệch nhau sẽ làm % tiến độ tốt nghiệp của MỌI sinh viên ngành đó sai.
    /// </summary>
    Task SyncMajorRequiredCreditsAsync(int majorId, CancellationToken cancellationToken = default);

    // ----- Prerequisite -----
    Task<int> AddPrerequisiteAsync(int courseId, int requiredCourseId, CancellationToken cancellationToken = default);
    Task RemovePrerequisiteAsync(int prerequisiteId, CancellationToken cancellationToken = default);
}
