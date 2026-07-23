namespace FAT.Services.Implementations;

/// <summary>
/// Cycle detection over the prerequisite graph.
///
/// WHY THIS MATTERS: the database only blocks a cycle of length one
/// (CK_Prerequisite_Self). Nothing stops A requiring B while B requires A, and
/// once that pair exists, resolving a prerequisite tree recurses forever and the
/// application hangs. Every write path - the admin screen and the importer -
/// checks here before inserting an edge, so the bad data never lands.
///
/// Pure and static, so the awkward cases (A-&gt;B-&gt;C-&gt;A, self-edges,
/// disconnected islands) are covered by plain unit tests with no database.
/// </summary>
internal static class PrerequisiteGraph
{
    /// <summary>
    /// Whether adding "<paramref name="courseId"/> requires
    /// <paramref name="requiredCourseId"/>" would close a loop.
    ///
    /// The new edge points from the required course up to the dependent one, so
    /// the question is whether the required course ALREADY depends - directly or
    /// through any chain - on the course we are about to gate. If it does, the
    /// edge closes a cycle.
    /// </summary>
    /// <param name="existingEdges">
    /// Current edges as (CourseId, RequiredCourseId) pairs.
    /// </param>
    public static bool WouldCreateCycle(
        IEnumerable<(int CourseId, int RequiredCourseId)> existingEdges,
        int courseId,
        int requiredCourseId)
    {
        // A course requiring itself is the degenerate cycle.
        if (courseId == requiredCourseId)
        {
            return true;
        }

        var requirementsOf = BuildAdjacency(existingEdges);

        // Walk everything requiredCourseId transitively depends on. Reaching
        // courseId means the new edge would close the loop.
        var visited = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(requiredCourseId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            // The visited set is what makes this terminate even when the
            // EXISTING data already contains a cycle - which it can, if a row
            // was inserted before this check was in place.
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == courseId)
            {
                return true;
            }

            if (!requirementsOf.TryGetValue(current, out var required))
            {
                continue;
            }

            foreach (var next in required)
            {
                pending.Push(next);
            }
        }

        return false;
    }

    /// <summary>
    /// Every course that <paramref name="courseId"/> transitively requires.
    /// Cycle-safe, and never includes the course itself.
    /// </summary>
    public static IReadOnlySet<int> GetAllRequirements(
        IEnumerable<(int CourseId, int RequiredCourseId)> edges,
        int courseId)
    {
        var requirementsOf = BuildAdjacency(edges);
        var result = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(courseId);
        var visited = new HashSet<int> { courseId };

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!requirementsOf.TryGetValue(current, out var required))
            {
                continue;
            }

            foreach (var next in required)
            {
                result.Add(next);

                if (visited.Add(next))
                {
                    pending.Push(next);
                }
            }
        }

        result.Remove(courseId);
        return result;
    }

    private static Dictionary<int, List<int>> BuildAdjacency(
        IEnumerable<(int CourseId, int RequiredCourseId)> edges)
    {
        var adjacency = new Dictionary<int, List<int>>();

        foreach (var (courseId, requiredCourseId) in edges)
        {
            if (!adjacency.TryGetValue(courseId, out var list))
            {
                list = [];
                adjacency[courseId] = list;
            }

            list.Add(requiredCourseId);
        }

        return adjacency;
    }
}
