using Data;
using Domain.Enums;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// Prerequisite checking.
///
/// A subject's requirements are AND-of-ORs:
///   GroupNo 0  -> each row is required in its own right
///   GroupNo &gt; 0 -> the rows sharing that number are ALTERNATIVES; passing any
///                  one of them satisfies the group
/// A subject unlocks when every group is satisfied. Treating the list as a flat
/// AND - which the original schema forced - would wrongly demand all three of
/// "MKT101 or MKG101 or MMK101".
/// </summary>
public sealed class PrerequisiteService : IPrerequisiteService
{
    private readonly FAT_DBContext _db;

    public PrerequisiteService(FAT_DBContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<CourseDto>> GetDirectPrerequisitesAsync(
        int courseId, CancellationToken cancellationToken = default)
        => await _db.Prerequisites
            .AsNoTracking()
            .Where(p => p.CourseId == courseId)
            .Select(p => p.RequiredCourse!)
            .Distinct()
            .OrderBy(c => c.CourseCode)
            .Select(c => new CourseDto(
                c.CourseId, c.CourseCode, c.CourseName, c.Credits, c.Description, c.IsActive,
                c.Prerequisites.Count, c.CountsTowardGpa, c.MinAvgMarkToPass,
                c.PrerequisiteText, c.SyllabusCode, c.SubjectMaterials.Count, c.Assessments.Count))
            .ToListAsync(cancellationToken);

    public async Task<PrerequisiteNodeDto> GetPrerequisiteTreeAsync(
        int courseId, CancellationToken cancellationToken = default)
    {
        var root = await _db.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(c => new { c.CourseId, c.CourseCode, c.CourseName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {courseId}.");

        // The whole graph in ONE query, then walked in memory. Querying per node
        // would be an N+1 that gets worse the deeper the chain runs
        // (PRN222 -> PRN212 -> PRO192 -> PRF192).
        var edges = await _db.Prerequisites
            .AsNoTracking()
            .Select(p => new
            {
                p.CourseId,
                p.RequiredCourseId,
                Code = p.RequiredCourse!.CourseCode,
                Name = p.RequiredCourse.CourseName
            })
            .ToListAsync(cancellationToken);

        var byCourse = edges
            .GroupBy(e => e.CourseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // The visited set is what makes bad data survivable: without it, an
        // A -> B -> A pair recurses until the process dies, and it dies during a
        // demo rather than in a test.
        var visited = new HashSet<int> { courseId };

        PrerequisiteNodeDto Build(int id, string code, string name, int depth)
        {
            var children = new List<PrerequisiteNodeDto>();

            if (byCourse.TryGetValue(id, out var required))
            {
                foreach (var edge in required.OrderBy(e => e.Code))
                {
                    if (!visited.Add(edge.RequiredCourseId))
                    {
                        continue;
                    }

                    children.Add(Build(edge.RequiredCourseId, edge.Code, edge.Name, depth + 1));
                }
            }

            return new PrerequisiteNodeDto(id, code, name, depth, children);
        }

        return Build(root.CourseId, root.CourseCode, root.CourseName, 0);
    }

    public async Task<PrerequisiteCheckResult> CanEnrollAsync(
        int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        var results = await CanEnrollManyAsync(studentId, [courseId], cancellationToken);

        return results.TryGetValue(courseId, out var result) ? result : PrerequisiteCheckResult.Ok();
    }

    public async Task<IReadOnlyDictionary<int, PrerequisiteCheckResult>> CanEnrollManyAsync(
        int studentId, IEnumerable<int> courseIds, CancellationToken cancellationToken = default)
    {
        var ids = courseIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
        {
            return new Dictionary<int, PrerequisiteCheckResult>();
        }

        // Two queries for the whole batch, not two per subject. This method is
        // called with an entire curriculum, so a per-subject round trip would
        // mean a hundred queries to draw one screen.
        var requirements = await _db.Prerequisites
            .AsNoTracking()
            .Where(p => ids.Contains(p.CourseId))
            .Select(p => new
            {
                p.CourseId,
                p.RequiredCourseId,
                p.GroupNo,
                p.Type,
                Code = p.RequiredCourse!.CourseCode,
                Name = p.RequiredCourse.CourseName
            })
            .ToListAsync(cancellationToken);

        var requiredCourseIds = requirements.Select(r => r.RequiredCourseId).Distinct().ToList();

        // The student's standing on each required subject. Grouped so a retaken
        // subject is judged on its BEST outcome - having failed something once
        // and passed it later must not keep the next subject locked.
        var standing = (await _db.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentId == studentId && requiredCourseIds.Contains(e.CourseId))
                .Select(e => new { e.CourseId, e.Status })
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(e => e.Status == EnrollmentStatus.Passed)
                    ? EnrollmentStatus.Passed
                    : g.Any(e => e.Status == EnrollmentStatus.Studying)
                        ? EnrollmentStatus.Studying
                        : g.First().Status);

        var results = new Dictionary<int, PrerequisiteCheckResult>(ids.Count);

        foreach (var id in ids)
        {
            var unmet = new List<UnmetPrerequisiteDto>();

            foreach (var group in requirements.Where(r => r.CourseId == id).GroupBy(r => r.GroupNo))
            {
                var members = group.ToList();

                // GroupNo 0 collects independent requirements, so each is judged
                // on its own. A positive GroupNo is a set of alternatives, and
                // one pass anywhere in it satisfies the whole group.
                if (group.Key == 0)
                {
                    unmet.AddRange(members
                        .Where(m => !IsSatisfied(standing, m.RequiredCourseId))
                        .Select(m => new UnmetPrerequisiteDto(
                            m.RequiredCourseId, m.Code, m.Name, m.Type, Standing(standing, m.RequiredCourseId))));

                    continue;
                }

                if (members.Any(m => IsSatisfied(standing, m.RequiredCourseId)))
                {
                    continue;
                }

                // Nothing in the group is passed: report every alternative so the
                // student can see the full choice, not just one arbitrary option.
                unmet.AddRange(members.Select(m => new UnmetPrerequisiteDto(
                    m.RequiredCourseId, m.Code, m.Name, m.Type, Standing(standing, m.RequiredCourseId))));
            }

            results[id] = unmet.Count == 0
                ? PrerequisiteCheckResult.Ok()
                : new PrerequisiteCheckResult(false, unmet);
        }

        return results;
    }

    /// <summary>
    /// A prerequisite counts as met only when the subject has been PASSED.
    /// "Currently studying" is not enough - the point of the rule is that the
    /// earlier subject is finished.
    /// </summary>
    private static bool IsSatisfied(IReadOnlyDictionary<int, EnrollmentStatus> standing, int courseId)
        => standing.TryGetValue(courseId, out var status) && status == EnrollmentStatus.Passed;

    private static EnrollmentStatus? Standing(
        IReadOnlyDictionary<int, EnrollmentStatus> standing, int courseId)
        => standing.TryGetValue(courseId, out var status) ? status : null;
}
