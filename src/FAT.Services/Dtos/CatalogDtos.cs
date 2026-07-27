namespace FAT.Services.Dtos;

/// <summary>A course with its prerequisite count, for lists and search.</summary>
public sealed record CourseDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    string? Description,
    bool IsActive,
    int PrerequisiteCount);

/// <summary>Filter applied on the course catalog screen.</summary>
public sealed record CourseFilter(
    string? Keyword = null,
    int? MinCredits = null,
    int? MaxCredits = null,
    int? MajorId = null,
    int? TermNo = null,
    bool? IsActive = true);

/// <summary>An academic term.</summary>
public sealed record SemesterDto(
    int SemesterId,
    string SemesterCode,
    string SemesterName,
    DateTime StartDate,
    DateTime EndDate,
    int DisplayOrder,
    bool IsCurrent);

/// <summary>One row of a degree curriculum.</summary>
public sealed record CurriculumItemDto(
    int CurriculumId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int TermNo,
    bool IsMandatory);

/// <summary>
/// A node in the prerequisite tree. Recursive so that multi-level chains can be
/// rendered (for example PRN222 -> PRN212 -> PRO192 -> PRF192).
/// </summary>
public sealed record PrerequisiteNodeDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Depth,
    IReadOnlyList<PrerequisiteNodeDto> Children);
