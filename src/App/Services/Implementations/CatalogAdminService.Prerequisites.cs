using Domain.Entities;
using Domain.Enums;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// Prerequisite editing.
///
/// Every write goes through <see cref="PrerequisiteGraph.WouldCreateCycle"/>.
/// The database only blocks a subject requiring ITSELF; a two-step loop
/// (A needs B, B needs A) passes every constraint and then hangs the
/// prerequisite tree forever.
/// </summary>
public sealed partial class CatalogAdminService
{
    public async Task<IReadOnlyList<PrerequisiteEdgeDto>> GetPrerequisitesAsync(
        int courseId, CancellationToken cancellationToken = default)
        => await _db.Prerequisites
            .AsNoTracking()
            .Where(p => p.CourseId == courseId)
            .OrderBy(p => p.GroupNo)
            .ThenBy(p => p.RequiredCourse!.CourseCode)
            .Select(p => new PrerequisiteEdgeDto(
                p.PrerequisiteId,
                p.CourseId,
                p.RequiredCourseId,
                p.RequiredCourse!.CourseCode,
                p.RequiredCourse.CourseName,
                p.GroupNo))
            .ToListAsync(cancellationToken);

    public async Task<int> AddPrerequisiteAsync(
        int courseId, int requiredCourseId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm môn tiên quyết");

        // GroupNo 0 - a requirement in its own right, AND-ed with the others.
        var created = await AddPrerequisiteRowsAsync(courseId, [requiredCourseId], groupNo: 0, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return created.Single().PrerequisiteId;
    }

    public async Task<int> AddPrerequisiteGroupAsync(
        int courseId, IReadOnlyList<int> requiredCourseIds, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm nhóm môn tiên quyết");

        if (requiredCourseIds is null || requiredCourseIds.Count == 0)
        {
            throw new ArgumentException("Danh sách môn tiên quyết không được để trống.", nameof(requiredCourseIds));
        }

        // A "group" of one is not a choice - store it as a plain requirement so
        // the eligibility check does not have to special-case it.
        if (requiredCourseIds.Count == 1)
        {
            return await AddPrerequisiteAsync(courseId, requiredCourseIds[0], cancellationToken);
        }

        var nextGroupNo = await _db.Prerequisites
            .Where(p => p.CourseId == courseId)
            .Select(p => (int?)p.GroupNo)
            .MaxAsync(cancellationToken) ?? 0;

        var groupNo = nextGroupNo + 1;

        await AddPrerequisiteRowsAsync(courseId, requiredCourseIds, groupNo, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return groupNo;
    }

    public async Task RemovePrerequisiteAsync(
        int prerequisiteId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa môn tiên quyết");

        var entity = await _db.Prerequisites
                .FirstOrDefaultAsync(p => p.PrerequisiteId == prerequisiteId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy điều kiện tiên quyết có mã định danh {prerequisiteId}.");

        _db.Prerequisites.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Validates and stages a set of prerequisite rows.
    /// Does not save - the caller owns the transaction.
    /// </summary>
    private async Task<IReadOnlyList<Prerequisite>> AddPrerequisiteRowsAsync(
        int courseId,
        IReadOnlyList<int> requiredCourseIds,
        int groupNo,
        CancellationToken cancellationToken)
    {
        var courseExists = await _db.Courses.AnyAsync(c => c.CourseId == courseId, cancellationToken);
        if (!courseExists)
        {
            throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {courseId}.");
        }

        var requestedIds = requiredCourseIds.Distinct().ToList();

        // Existence and duplicate checks resolved in TWO queries for the whole
        // batch rather than two per alternative. A choice group is small, but
        // the per-iteration version is the same N+1 shape that becomes a
        // problem the moment anything calls this in a loop.
        var knownCourseIds = (await _db.Courses
                .Where(c => requestedIds.Contains(c.CourseId))
                .Select(c => c.CourseId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var existingPairs = (await _db.Prerequisites
                .Where(p => p.CourseId == courseId && requestedIds.Contains(p.RequiredCourseId))
                .Select(p => p.RequiredCourseId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // The whole graph, not just this subject's edges: a new edge can close a
        // loop through subjects the caller never mentioned.
        var edges = (await _db.Prerequisites
                .Select(p => new { p.CourseId, p.RequiredCourseId })
                .ToListAsync(cancellationToken))
            .Select(p => (p.CourseId, p.RequiredCourseId))
            .ToList();

        var created = new List<Prerequisite>();

        foreach (var requiredCourseId in requestedIds)
        {
            // Mirrors CK_Prerequisite_Self.
            if (requiredCourseId == courseId)
            {
                throw new InvalidOperationException("Một môn học không thể là môn tiên quyết của chính nó.");
            }

            if (!knownCourseIds.Contains(requiredCourseId))
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy môn tiên quyết có mã định danh {requiredCourseId}.");
            }

            if (existingPairs.Contains(requiredCourseId))
            {
                throw new InvalidOperationException("Điều kiện tiên quyết này đã tồn tại.");
            }

            if (PrerequisiteGraph.WouldCreateCycle(edges, courseId, requiredCourseId))
            {
                throw new InvalidOperationException(
                    "Không thể thêm điều kiện tiên quyết này vì sẽ tạo thành vòng lặp trong cây môn tiên quyết.");
            }

            var entity = new Prerequisite
            {
                CourseId = courseId,
                RequiredCourseId = requiredCourseId,
                Type = PrerequisiteType.Prerequisite,
                GroupNo = groupNo
            };

            _db.Prerequisites.Add(entity);
            created.Add(entity);

            // Fold the new edge in so the next one in this batch is checked
            // against it too - otherwise two edges added together could close a
            // loop that neither closes alone.
            edges.Add((courseId, requiredCourseId));
        }

        return created;
    }
}
