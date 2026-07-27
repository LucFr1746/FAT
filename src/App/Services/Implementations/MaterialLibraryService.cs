using Data;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// The material library shown to students - one searchable list per major.
///
/// It merges two sources into <see cref="MaterialLibraryItemDto"/>:
///   * SubjectMaterial - the FLM syllabus links (a title plus a URL), and
///   * Material        - files an admin uploaded (bytes live in MaterialFile).
///
/// The list carries no bytes: a link row opens its URL, an uploaded row is
/// fetched on demand through <see cref="IMaterialService.DownloadAsync"/>. The
/// whole list is scoped to the signed-in student's major (SE sees SE, AI sees
/// AI); an admin, having no student profile, sees every subject.
/// </summary>
public sealed class MaterialLibraryService : IMaterialLibraryService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;

    public MaterialLibraryService(FAT_DBContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<MaterialLibraryItemDto>> SearchAsync(
        MaterialLibraryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var scopeCourseIds = await GetScopeCourseIdsAsync(filter.MajorId, filter.TermNo, cancellationToken);
        var keyword = string.IsNullOrWhiteSpace(filter.Keyword) ? null : filter.Keyword.Trim();

        // ---- FLM links (SubjectMaterial) ----
        var linkQuery = _db.SubjectMaterials.AsNoTracking().Where(m => m.IsActive);

        if (scopeCourseIds is not null)
        {
            linkQuery = linkQuery.Where(m => scopeCourseIds.Contains(m.CourseId));
        }

        if (filter.CourseId is int linkCourse)
        {
            linkQuery = linkQuery.Where(m => m.CourseId == linkCourse);
        }

        if (filter.OnlyDownloadable)
        {
            linkQuery = linkQuery.Where(m => m.Url != null && m.Url != "");
        }

        if (keyword is not null)
        {
            linkQuery = linkQuery.Where(m =>
                EF.Functions.Like(m.Title, $"%{keyword}%")
                || (m.Author != null && EF.Functions.Like(m.Author, $"%{keyword}%"))
                || (m.Publisher != null && EF.Functions.Like(m.Publisher, $"%{keyword}%"))
                || EF.Functions.Like(m.Course!.CourseCode, $"%{keyword}%")
                || EF.Functions.Like(m.Course!.CourseName, $"%{keyword}%"));
        }

        var links = await linkQuery
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Title)
            .Select(m => new MaterialLibraryItemDto(
                m.SubjectMaterialId, null, m.CourseId, m.Course!.CourseCode, m.Course!.CourseName,
                m.Title, m.Description, m.Url, m.Author, m.Publisher, m.Isbn, null, null))
            .ToListAsync(cancellationToken);

        // ---- Uploaded files (Material). Only rows that actually have a file and
        // are tied to a subject can appear in a per-subject library. ----
        var fileQuery = _db.Materials.AsNoTracking()
            .Where(m => m.IsActive && m.CourseId != null && m.File != null);

        if (scopeCourseIds is not null)
        {
            fileQuery = fileQuery.Where(m => scopeCourseIds.Contains(m.CourseId!.Value));
        }

        if (filter.CourseId is int fileCourse)
        {
            fileQuery = fileQuery.Where(m => m.CourseId == fileCourse);
        }

        // An uploaded file is always downloadable, so OnlyDownloadable never hides it.

        if (keyword is not null)
        {
            fileQuery = fileQuery.Where(m =>
                EF.Functions.Like(m.Title, $"%{keyword}%")
                || EF.Functions.Like(m.FileName, $"%{keyword}%")
                || EF.Functions.Like(m.Course!.CourseCode, $"%{keyword}%")
                || EF.Functions.Like(m.Course!.CourseName, $"%{keyword}%"));
        }

        var files = await fileQuery
            .OrderByDescending(m => m.UploadedAt)
            .Select(m => new MaterialLibraryItemDto(
                0, m.MaterialId, m.CourseId!.Value, m.Course!.CourseCode, m.Course!.CourseName,
                m.Title, m.Description, null, null, null, null, m.FileName, m.FileSizeBytes))
            .ToListAsync(cancellationToken);

        // Stable OrderBy keeps each source's ordering within a subject: links
        // (by display order) first, then uploaded files (newest first).
        return links.Concat(files)
            .OrderBy(x => x.CourseCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<MaterialSubjectOptionDto>> GetSubjectOptionsAsync(
        int? majorId = null, CancellationToken cancellationToken = default)
    {
        var scopeCourseIds = await GetScopeCourseIdsAsync(majorId, null, cancellationToken);

        var linkQuery = _db.SubjectMaterials.AsNoTracking().Where(m => m.IsActive);
        var fileQuery = _db.Materials.AsNoTracking().Where(m => m.IsActive && m.CourseId != null && m.File != null);

        if (scopeCourseIds is not null)
        {
            linkQuery = linkQuery.Where(m => scopeCourseIds.Contains(m.CourseId));
            fileQuery = fileQuery.Where(m => scopeCourseIds.Contains(m.CourseId!.Value));
        }

        // Distinct over anonymous primitives translates to a clean SELECT DISTINCT;
        // the DTO is built in memory.
        var fromLinks = await linkQuery
            .Select(m => new { m.CourseId, m.Course!.CourseCode, m.Course!.CourseName })
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromFiles = await fileQuery
            .Select(m => new { CourseId = m.CourseId!.Value, m.Course!.CourseCode, m.Course!.CourseName })
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromLinks.Concat(fromFiles)
            .GroupBy(x => x.CourseId)
            .Select(g => g.First())
            .OrderBy(x => x.CourseCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MaterialSubjectOptionDto(x.CourseId, x.CourseCode, x.CourseName))
            .ToList();
    }

    /// <summary>
    /// The course ids a query should be limited to, combining major and term:
    ///   * a signed-in student is always locked to their own major (an admin uses
    ///     the major they picked, if any), and
    ///   * the chosen term, if any.
    ///
    /// Returns null when nothing narrows the query. Both scopes resolve through
    /// the Curriculum table.
    /// </summary>
    private async Task<IReadOnlyList<int>?> GetScopeCourseIdsAsync(
        int? filterMajorId, int? termNo, CancellationToken cancellationToken)
    {
        int? effectiveMajorId;
        if (_currentUser.StudentId is int studentId)
        {
            // A student can never widen past their own major.
            effectiveMajorId = await _db.Students
                .AsNoTracking()
                .Where(s => s.StudentId == studentId)
                .Select(s => (int?)s.MajorId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            effectiveMajorId = filterMajorId;
        }

        if (effectiveMajorId is null && termNo is null)
        {
            return null; // Admin with neither major nor term chosen: every subject.
        }

        var query = _db.CurriculumItems.AsNoTracking();

        if (effectiveMajorId is int mid)
        {
            query = query.Where(c => c.MajorId == mid);
        }

        if (termNo is int term)
        {
            query = query.Where(c => c.TermNo == term);
        }

        return await query
            .Select(c => c.CourseId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialMajorOptionDto>> GetMajorOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Majors
            .AsNoTracking()
            .OrderBy(m => m.MajorCode)
            .Select(m => new { m.MajorId, m.MajorCode, m.MajorName })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new MaterialMajorOptionDto(x.MajorId, x.MajorCode, x.MajorName))
            .ToList();
    }
}
