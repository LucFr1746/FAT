namespace FAT.Services.Dtos;

/// <summary>
/// A course with its prerequisite count, for lists and search.
///
/// The trailing parameters have defaults so that the older call sites written
/// against the original seven-field contract keep compiling.
/// </summary>
public sealed record CourseDto(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    string? Description,
    bool IsActive,
    int PrerequisiteCount,
    bool CountsTowardGpa = true,
    decimal? MinAvgMarkToPass = null,
    string? PrerequisiteText = null,
    string? SyllabusCode = null,
    int MaterialCount = 0,
    int AssessmentCount = 0)
{
    /// <summary>"Có"/"Không", matching the wording FLM uses.</summary>
    public string GpaDisplay => CountsTowardGpa ? "Có" : "Không";
}

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

/// <summary>
/// One row of a degree curriculum.
///
/// The trailing parameters have defaults so the original seven-field contract
/// keeps compiling.
/// </summary>
public sealed record CurriculumItemDto(
    int CurriculumId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int Credits,
    int TermNo,
    bool IsMandatory,
    int DisplayOrder = 0,
    bool CountsTowardGpa = true,
    int MajorId = 0,
    string? MajorCode = null,
    string? TermName = null);

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
