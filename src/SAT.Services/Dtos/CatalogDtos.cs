namespace SAT.Services.Dtos;

/// <summary>Môn học kèm số môn tiên quyết, dùng cho danh sách và tìm kiếm.</summary>
public sealed record CourseDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    string? Description,
    bool IsActive,
    int PrerequisiteCount);

/// <summary>Bộ lọc của màn hình danh mục môn học.</summary>
public sealed record CourseFilter(
    string? Keyword = null,
    int? MinCredits = null,
    int? MaxCredits = null,
    int? MajorId = null,
    int? TermNo = null,
    bool? IsActive = true);

/// <summary>Học kỳ.</summary>
public sealed record SemesterDto(
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    DateTime StartDate,
    DateTime EndDate,
    int DisplayOrder,
    bool IsCurrent);

/// <summary>Một dòng trong khung chương trình đào tạo.</summary>
public sealed record CurriculumItemDto(
    int CurriculumId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int TermNo,
    bool IsMandatory);

/// <summary>
/// Một nút trong cây môn tiên quyết. Đệ quy để vẽ được đồ thị nhiều tầng
/// (ví dụ PRN222 -> PRN212 -> PRO192 -> PRF192).
/// </summary>
public sealed record PrerequisiteNodeDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Depth,
    IReadOnlyList<PrerequisiteNodeDto> Children);
