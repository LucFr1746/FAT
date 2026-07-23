using System.Linq.Expressions;
using FAT.Domain.Constants;
using FAT.Domain.Entities;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>Manage Subject - the subject catalog itself.</summary>
public sealed partial class CatalogAdminService
{
    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
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

        // Filtering by programme or kỳ goes through the curriculum link, because
        // that is where those facts live - a subject sits in a different kỳ
        // depending on the programme.
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

    public async Task<CourseDto?> GetCourseAsync(int courseId, CancellationToken cancellationToken = default)
        => await _db.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(CourseProjection)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CreateCourseAsync(CourseDto course, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm môn học");

        var code = Require(course.CourseCode, "Mã môn học", CatalogRules.CourseCodeMaxLength)
            .ToUpperInvariant();
        var name = Require(course.CourseName, "Tên môn học", CatalogRules.CourseNameMaxLength);

        ValidateCourseValues(course);
        await EnsureCourseCodeIsFreeAsync(code, excludeCourseId: null, cancellationToken);

        var entity = new Course
        {
            CourseCode = code,
            CourseName = name,
            Credits = course.Credits,
            Description = course.Description?.Trim(),
            CountsTowardGpa = course.CountsTowardGpa,
            MinAvgMarkToPass = course.MinAvgMarkToPass,
            PrerequisiteText = course.PrerequisiteText?.Trim(),
            SyllabusCode = course.SyllabusCode?.Trim(),
            IsActive = course.IsActive
        };

        _db.Courses.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.CourseId;
    }

    public async Task UpdateCourseAsync(CourseDto course, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật môn học");

        var code = Require(course.CourseCode, "Mã môn học", CatalogRules.CourseCodeMaxLength)
            .ToUpperInvariant();
        var name = Require(course.CourseName, "Tên môn học", CatalogRules.CourseNameMaxLength);

        ValidateCourseValues(course);

        var entity = await _db.Courses.FirstOrDefaultAsync(c => c.CourseId == course.CourseId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {course.CourseId}.");

        await EnsureCourseCodeIsFreeAsync(code, course.CourseId, cancellationToken);

        var creditsChanged = entity.Credits != course.Credits;

        // Changing a subject's credits changes the total of EVERY programme that
        // teaches it. Missing that is the subtle way RequiredCredits drifts, so
        // the resync runs through ApplyAndSyncAsync, which guarantees it happens
        // after the new credits are actually persisted.
        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                entity.CourseCode = code;
                entity.CourseName = name;
                entity.Credits = course.Credits;
                entity.Description = course.Description?.Trim();
                entity.CountsTowardGpa = course.CountsTowardGpa;
                entity.MinAvgMarkToPass = course.MinAvgMarkToPass;
                entity.PrerequisiteText = course.PrerequisiteText?.Trim();
                entity.SyllabusCode = course.SyllabusCode?.Trim();
                entity.IsActive = course.IsActive;
                return Task.CompletedTask;
            },
            getAffectedMajorIds: async () => creditsChanged
                ? await _db.CurriculumItems
                    .Where(ci => ci.CourseId == course.CourseId)
                    .Select(ci => ci.MajorId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                : [],
            cancellationToken);
    }

    public async Task DeactivateCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Ngừng hoạt động môn học");

        var entity = await _db.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {courseId}.");

        // Deactivating a subject people are sitting right now hides it from the
        // screens they need mid-semester.
        var studying = await _db.Enrollments
            .CountAsync(e => e.CourseId == courseId && e.Status == EnrollmentStatus.Studying, cancellationToken);

        if (studying > 0)
        {
            throw new InvalidOperationException(
                $"Không thể ngừng hoạt động môn '{entity.CourseCode}': còn {studying} lượt đăng ký đang học.");
        }

        // Deactivated, never deleted: a hard delete would take the transcript of
        // every student who ever took the subject with it.
        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Projection shared by the list and detail queries, so the two can never
    /// disagree about what a subject looks like.
    ///
    /// An Expression rather than a method: EF Core cannot translate an ordinary
    /// method call inside a Select, and would silently fall back to evaluating
    /// it in memory - pulling every column of every row across the wire.
    ///
    /// Every constructor argument is passed explicitly because an expression
    /// tree may not rely on a record's default parameter values.
    /// </summary>
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

    private static void ValidateCourseValues(CourseDto course)
    {
        // Mirrors CK_Course_Credits.
        if (course.Credits < CatalogRules.MinCredits || course.Credits > CatalogRules.MaxCredits)
        {
            throw new ArgumentException(
                $"Số tín chỉ phải nằm trong khoảng {CatalogRules.MinCredits} đến {CatalogRules.MaxCredits}.",
                nameof(course));
        }

        if (course.MinAvgMarkToPass is < 0 or > 10)
        {
            throw new ArgumentException("Điểm tối thiểu để qua môn phải nằm trong khoảng 0 đến 10.", nameof(course));
        }
    }

    private async Task EnsureCourseCodeIsFreeAsync(
        string courseCode, int? excludeCourseId, CancellationToken cancellationToken)
    {
        var taken = await _db.Courses.AnyAsync(
            c => c.CourseCode == courseCode && (excludeCourseId == null || c.CourseId != excludeCourseId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Mã môn học '{courseCode}' đã tồn tại.");
        }
    }
}
