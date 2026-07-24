using System.Globalization;
using System.Text;
using Data;
using Domain.Constants;
using Domain.Entities;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// Assign Subject to Major, and Curriculum Management.
///
/// EVERY mutating method finishes by resynchronising Major.RequiredCredits in
/// the same SaveChanges. That column is the denominator of the graduation
/// percentage; letting it drift makes the progress bar wrong for every student
/// in the programme, with nothing in the UI to explain it.
/// </summary>
public sealed class CurriculumAdminService : ICurriculumAdminService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;

    public CurriculumAdminService(FAT_DBContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    // =========================================================================
    // Reads
    // =========================================================================

    public async Task<IReadOnlyList<CurriculumItemDto>> GetByMajorAsync(
        int majorId, int? termNo = null, CancellationToken cancellationToken = default)
        => await SearchAsync(new CurriculumFilter(MajorId: majorId, TermNo: termNo), cancellationToken);

    public async Task<IReadOnlyList<CurriculumItemDto>> GetByTermAsync(
        int termNo, CancellationToken cancellationToken = default)
        => await SearchAsync(new CurriculumFilter(TermNo: termNo), cancellationToken);

    public async Task<IReadOnlyList<CurriculumItemDto>> SearchAsync(
        CurriculumFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.CurriculumItems.AsNoTracking();

        if (filter.MajorId is not null)
        {
            query = query.Where(ci => ci.MajorId == filter.MajorId);
        }

        if (filter.TermNo is not null)
        {
            query = query.Where(ci => ci.TermNo == filter.TermNo);
        }

        if (filter.IsMandatory is not null)
        {
            query = query.Where(ci => ci.IsMandatory == filter.IsMandatory);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(ci => EF.Functions.Like(ci.Course!.CourseCode, $"%{keyword}%")
                                   || EF.Functions.Like(ci.Course!.CourseName, $"%{keyword}%"));
        }

        // Kỳ first, then the stored order within it - the sequence the reorder
        // feature maintains and the one students read the curriculum in.
        return await query
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

    public async Task<IReadOnlyList<CourseDto>> GetUnassignedCoursesAsync(
        int majorId, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Courses
            .AsNoTracking()
            .Where(c => c.IsActive && !c.CurriculumItems.Any(ci => ci.MajorId == majorId));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(c => EF.Functions.Like(c.CourseCode, $"%{trimmed}%")
                                  || EF.Functions.Like(c.CourseName, $"%{trimmed}%"));
        }

        return await query
            .OrderBy(c => c.CourseCode)
            // Every argument is spelled out: an expression tree may not rely on
            // a record's default parameter values.
            .Select(c => new CourseDto(
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
                c.Assessments.Count))
            .ToListAsync(cancellationToken);
    }

    // =========================================================================
    // Writes
    // =========================================================================

    public async Task<int> AssignAsync(
        int majorId, int courseId, int termNo, bool isMandatory = true,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Gán môn học vào ngành");

        await ValidateTermAsync(termNo, cancellationToken);
        await ValidateMajorAndCourseAsync(majorId, courseId, cancellationToken);

        var duplicate = await _db.CurriculumItems
            .AnyAsync(ci => ci.MajorId == majorId && ci.CourseId == courseId, cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException("Môn học này đã có trong chương trình học của ngành.");
        }

        var item = new Curriculum
        {
            MajorId = majorId,
            CourseId = courseId,
            TermNo = termNo,
            DisplayOrder = await GetNextDisplayOrderAsync(majorId, termNo, cancellationToken),
            IsMandatory = isMandatory
        };

        // The resync must see the NEW row, so it has to run after the insert is
        // saved - which is exactly what ApplyAndSyncAsync guarantees.
        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                _db.CurriculumItems.Add(item);
                return Task.CompletedTask;
            },
            getAffectedMajorIds: () => Task.FromResult<IReadOnlyList<int>>([majorId]),
            cancellationToken);

        return item.CurriculumId;
    }

    public async Task<BulkOperationResultDto> BulkAssignAsync(
        int majorId, IReadOnlyList<int> courseIds, int termNo, bool isMandatory = true,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Gán nhiều môn học vào ngành");

        if (courseIds is null || courseIds.Count == 0)
        {
            return new BulkOperationResultDto(0, 0, ["Chưa chọn môn học nào."]);
        }

        await ValidateTermAsync(termNo, cancellationToken);

        var distinctIds = courseIds.Distinct().ToList();

        // One query for the whole batch rather than one per subject: assigning
        // forty subjects should not mean eighty round trips.
        var alreadyAssigned = await _db.CurriculumItems
            .Where(ci => ci.MajorId == majorId && distinctIds.Contains(ci.CourseId))
            .Select(ci => ci.CourseId)
            .ToListAsync(cancellationToken);

        var existingCourses = await _db.Courses
            .Where(c => distinctIds.Contains(c.CourseId))
            .Select(c => new { c.CourseId, c.CourseCode })
            .ToListAsync(cancellationToken);

        var knownIds = existingCourses.Select(c => c.CourseId).ToHashSet();
        var messages = new List<string>();
        var succeeded = 0;
        var skipped = 0;
        var displayOrder = await GetNextDisplayOrderAsync(majorId, termNo, cancellationToken);

        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                foreach (var courseId in distinctIds)
                {
                    if (!knownIds.Contains(courseId))
                    {
                        skipped++;
                        messages.Add($"Bỏ qua môn có mã định danh {courseId}: không tồn tại.");
                        continue;
                    }

                    // Skipped, not fatal: one accidental selection must not
                    // abandon the rest of the batch.
                    if (alreadyAssigned.Contains(courseId))
                    {
                        skipped++;
                        var code = existingCourses.First(c => c.CourseId == courseId).CourseCode;
                        messages.Add($"Bỏ qua môn {code}: đã có trong chương trình học.");
                        continue;
                    }

                    _db.CurriculumItems.Add(new Curriculum
                    {
                        MajorId = majorId,
                        CourseId = courseId,
                        TermNo = termNo,
                        DisplayOrder = displayOrder++,
                        IsMandatory = isMandatory
                    });

                    succeeded++;
                }

                return Task.CompletedTask;
            },
            getAffectedMajorIds: () => Task.FromResult<IReadOnlyList<int>>(
                succeeded > 0 ? [majorId] : []),
            cancellationToken);

        return new BulkOperationResultDto(succeeded, skipped, messages);
    }

    public async Task RemoveAsync(int curriculumId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa môn học khỏi chương trình");

        var item = await _db.CurriculumItems
                .FirstOrDefaultAsync(ci => ci.CurriculumId == curriculumId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy mục chương trình học có mã định danh {curriculumId}.");

        var majorId = item.MajorId;

        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                _db.CurriculumItems.Remove(item);
                return Task.CompletedTask;
            },
            getAffectedMajorIds: () => Task.FromResult<IReadOnlyList<int>>([majorId]),
            cancellationToken);
    }

    public async Task<BulkOperationResultDto> BulkRemoveAsync(
        IReadOnlyList<int> curriculumIds, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa nhiều môn học khỏi chương trình");

        if (curriculumIds is null || curriculumIds.Count == 0)
        {
            return new BulkOperationResultDto(0, 0, ["Chưa chọn mục nào."]);
        }

        var distinctIds = curriculumIds.Distinct().ToList();

        var items = await _db.CurriculumItems
            .Where(ci => distinctIds.Contains(ci.CurriculumId))
            .ToListAsync(cancellationToken);

        var affectedMajorIds = items.Select(i => i.MajorId).Distinct().ToList();

        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                _db.CurriculumItems.RemoveRange(items);
                return Task.CompletedTask;
            },
            getAffectedMajorIds: () => Task.FromResult<IReadOnlyList<int>>(affectedMajorIds),
            cancellationToken);

        var skipped = distinctIds.Count - items.Count;
        var messages = skipped > 0
            ? new List<string> { $"Bỏ qua {skipped} mục không tồn tại." }
            : [];

        return new BulkOperationResultDto(items.Count, skipped, messages);
    }

    public async Task UpdateItemAsync(
        int curriculumId, int termNo, bool isMandatory, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật mục chương trình học");

        await ValidateTermAsync(termNo, cancellationToken);

        var item = await _db.CurriculumItems
                .FirstOrDefaultAsync(ci => ci.CurriculumId == curriculumId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy mục chương trình học có mã định danh {curriculumId}.");

        var majorId = item.MajorId;
        var nextOrder = item.TermNo != termNo
            ? await GetNextDisplayOrderAsync(majorId, termNo, cancellationToken)
            : item.DisplayOrder;

        await MajorCreditCalculator.ApplyAndSyncAsync(
            _db,
            applyChange: () =>
            {
                // Moving to another kỳ means taking a position at the end of that
                // kỳ; keeping the old index would collide with whatever holds it.
                item.DisplayOrder = nextOrder;
                item.TermNo = termNo;
                item.IsMandatory = isMandatory;
                return Task.CompletedTask;
            },
            // TotalTerms is derived from the highest kỳ in use, so moving a
            // subject between kỳ can change it even though credits did not.
            getAffectedMajorIds: () => Task.FromResult<IReadOnlyList<int>>([majorId]),
            cancellationToken);
    }

    public async Task ReorderAsync(
        int majorId, int termNo, IReadOnlyList<int> orderedCurriculumIds,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Sắp xếp lại chương trình học");

        if (orderedCurriculumIds is null || orderedCurriculumIds.Count == 0)
        {
            return;
        }

        var items = await _db.CurriculumItems
            .Where(ci => ci.MajorId == majorId && ci.TermNo == termNo)
            .ToListAsync(cancellationToken);

        // The caller must send the whole kỳ. A partial list would renumber the
        // items it mentions and leave the rest on stale positions, producing
        // duplicate orders and an arbitrary display sequence.
        var providedIds = orderedCurriculumIds.Distinct().ToList();
        var actualIds = items.Select(i => i.CurriculumId).ToList();

        if (providedIds.Count != actualIds.Count || providedIds.Except(actualIds).Any())
        {
            throw new InvalidOperationException(
                "Danh sách sắp xếp phải chứa đầy đủ và chính xác các môn học của kỳ này.");
        }

        for (var index = 0; index < providedIds.Count; index++)
        {
            items.First(i => i.CurriculumId == providedIds[index]).DisplayOrder = index;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // =========================================================================
    // Export
    // =========================================================================

    public async Task<string> ExportToCsvAsync(int majorId, CancellationToken cancellationToken = default)
    {
        var major = await _db.Majors.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MajorId == majorId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy ngành học có mã định danh {majorId}.");

        var items = await GetByMajorAsync(majorId, cancellationToken: cancellationToken);

        var builder = new StringBuilder();

        // UTF-8 BOM so Excel opens the Vietnamese text correctly instead of
        // rendering it as mojibake.
        builder.Append('﻿');
        builder.AppendLine("MaNganh,TenNganh,Ky,ThuTu,MaMon,TenMon,SoTinChi,TinhGPA,BatBuoc");

        foreach (var item in items)
        {
            builder.AppendLine(string.Join(',',
                Escape(major.MajorCode),
                Escape(major.MajorName),
                item.TermNo.ToString(CultureInfo.InvariantCulture),
                item.DisplayOrder.ToString(CultureInfo.InvariantCulture),
                Escape(item.CourseCode),
                Escape(item.CourseName),
                item.Credits.ToString(CultureInfo.InvariantCulture),
                item.CountsTowardGpa ? "Có" : "Không",
                item.IsMandatory ? "Có" : "Không"));
        }

        return builder.ToString();
    }

    /// <summary>Quotes a CSV field; subject names contain commas often enough to matter.</summary>
    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;

        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Next free position at the end of a kỳ.</summary>
    private async Task<int> GetNextDisplayOrderAsync(
        int majorId, int termNo, CancellationToken cancellationToken)
    {
        var max = await _db.CurriculumItems
            .Where(ci => ci.MajorId == majorId && ci.TermNo == termNo)
            .Select(ci => (int?)ci.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (max ?? -1) + 1;
    }

    /// <summary>
    /// Confirms the kỳ exists before it is used.
    /// Curriculum.TermNo is a foreign key onto Term.TermNo, so a missing kỳ
    /// would otherwise surface as a raw constraint violation.
    /// </summary>
    private async Task ValidateTermAsync(int termNo, CancellationToken cancellationToken)
    {
        if (termNo < CatalogRules.MinTermNo || termNo > CatalogRules.MaxTermNo)
        {
            throw new ArgumentException(
                $"Kỳ học phải nằm trong khoảng {CatalogRules.MinTermNo} đến {CatalogRules.MaxTermNo}.",
                nameof(termNo));
        }

        var exists = await _db.Terms.AnyAsync(t => t.TermNo == termNo, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Kỳ {termNo} chưa được khai báo. Hãy thêm kỳ này ở màn hình Quản lý kỳ học trước.");
        }
    }

    private async Task ValidateMajorAndCourseAsync(
        int majorId, int courseId, CancellationToken cancellationToken)
    {
        if (!await _db.Majors.AnyAsync(m => m.MajorId == majorId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy ngành học có mã định danh {majorId}.");
        }

        if (!await _db.Courses.AnyAsync(c => c.CourseId == courseId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {courseId}.");
        }
    }
}
