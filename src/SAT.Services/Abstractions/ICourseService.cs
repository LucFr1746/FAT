using SAT.Services.Dtos;

namespace SAT.Services.Abstractions;

/// <summary>
/// Tra cứu danh mục - CHỈ ĐỌC.
/// 🔒 Hợp đồng đóng băng Day 1 - chủ sở hữu: Member 3.
///
/// Phục vụ 4/5 chức năng của Member 3: Select Major, View Subjects,
/// View Semester, Subject Detail.
///
/// TÁCH ĐỌC / GHI LÀ CÓ CHỦ ĐÍCH: mọi thao tác thay đổi danh mục nằm ở
/// <see cref="ICatalogAdminService"/> của Member 2. Nhờ vậy hai người làm
/// cùng một vùng dữ liệu nhưng sửa hai file khác nhau, gần như không đụng
/// nhau khi merge.
/// </summary>
public interface ICourseService
{
    /// <summary>View Subjects + tìm kiếm, lọc.</summary>
    Task<IReadOnlyList<CourseDto>> SearchAsync(CourseFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Subject Detail.</summary>
    Task<CourseDto?> GetByIdAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>Select Major - danh sách ngành để chọn.</summary>
    Task<IReadOnlyList<MajorDto>> GetMajorsAsync(CancellationToken cancellationToken = default);

    /// <summary>View Semester.</summary>
    Task<IReadOnlyList<SemesterDto>> GetSemestersAsync(CancellationToken cancellationToken = default);

    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken cancellationToken = default);

    /// <summary>Khung chương trình của một ngành, sắp theo kỳ.</summary>
    Task<IReadOnlyList<CurriculumItemDto>> GetCurriculumAsync(int majorId, CancellationToken cancellationToken = default);
}
