using System.Linq.Expressions;
using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Catalog lookups - READ ONLY. Every mutation lives in
/// <see cref="CatalogAdminService"/> and <see cref="CurriculumAdminService"/>.
///
/// No authorization guard here on purpose: a signed-in student is entitled to
/// browse the catalog. Nothing this service returns is specific to one student.
/// </summary>
public sealed class CourseService : ICourseService
{
    private readonly FAT_DBContext _db;

    public CourseService(FAT_DBContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>Shared projection - see the note on CatalogAdminService.CourseProjection.</summary>
    private static readonly Expression<Func<Course, CourseDto>> CourseProjection =
        c => new CourseDto(
            c.CourseId,
            c.CourseCode,
            c.CourseName,
            c.Credits,
            c.Description,
            c.IsActive,
            c.Prerequisites.Count,
            c.CountsTowardGpa,
            c.MinAvgMarkToPass,
            c.PrerequisiteText,
            c.SyllabusCode,
            c.SubjectMaterials.Count,
            c.Assessments.Count);

    public async Task<IReadOnlyList<CourseDto>> SearchAsync(
        CourseFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Courses.AsNoTracking();

        if (filter.IsActive is not null)
        {
            query = query.Where(c => c.IsActive == filter.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(c => EF.Functions.Like(c.CourseCode, $"%{keyword}%")
                                  || EF.Functions.Like(c.CourseName, $"%{keyword}%"));
        }

        if (filter.MinCredits is not null)
        {
            query = query.Where(c => c.Credits >= filter.MinCredits);
        }

        if (filter.MaxCredits is not null)
        {
            query = query.Where(c => c.Credits <= filter.MaxCredits);
        }

        if (filter.MajorId is not null)
        {
            query = query.Where(c => c.CurriculumItems.Any(ci => ci.MajorId == filter.MajorId));
        }

        if (filter.TermNo is not null)
        {
            query = query.Where(c => c.CurriculumItems.Any(
                ci => ci.TermNo == filter.TermNo
                      && (filter.MajorId == null || ci.MajorId == filter.MajorId)));
        }

        return await query
            .OrderBy(c => c.CourseCode)
            .Select(CourseProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<CourseDto?> GetByIdAsync(int courseId, CancellationToken cancellationToken = default)
        => await _db.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(CourseProjection)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MajorDto>> GetMajorsAsync(CancellationToken cancellationToken = default)
        => await _db.Majors
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.MajorCode)
            .Select(m => new MajorDto(
                m.MajorId, m.MajorCode, m.MajorName, m.RequiredCredits, m.TotalTerms, m.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SemesterDto>> GetSemestersAsync(
        CancellationToken cancellationToken = default)
        => await _db.Semesters
            .AsNoTracking()
            // DisplayOrder, never SemesterCode: "FA25" sorts before "SP26"
            // alphabetically even though it happens first, which would put the
            // GPA trend chart in the wrong order.
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SemesterDto(
                s.SemesterId, s.SemesterCode, s.SemesterName,
                s.StartDate, s.EndDate, s.DisplayOrder, s.IsCurrent))
            .ToListAsync(cancellationToken);

    public async Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken cancellationToken = default)
        => await _db.Semesters
            .AsNoTracking()
            .Where(s => s.IsCurrent)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SemesterDto(
                s.SemesterId, s.SemesterCode, s.SemesterName,
                s.StartDate, s.EndDate, s.DisplayOrder, s.IsCurrent))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CurriculumItemDto>> GetCurriculumAsync(
        int majorId, CancellationToken cancellationToken = default)
        => await _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.MajorId == majorId)
            .OrderBy(ci => ci.TermNo)
            .ThenBy(ci => ci.DisplayOrder)
            .ThenBy(ci => ci.Course!.CourseCode)
            .Select(ci => new CurriculumItemDto(
                ci.CurriculumId,
                ci.CourseId,
                ci.Course!.CourseCode,
                ci.Course.CourseName,
                ci.Course.Credits,
                ci.TermNo,
                ci.IsMandatory,
                ci.DisplayOrder,
                ci.Course.CountsTowardGpa,
                ci.MajorId,
                ci.Major!.MajorCode,
                ci.Term!.TermName))
            .ToListAsync(cancellationToken);
}
