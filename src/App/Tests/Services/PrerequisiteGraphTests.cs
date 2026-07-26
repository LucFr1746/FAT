using FluentAssertions;
using Services.Implementations;

namespace Tests.Services;

/// <summary>
/// Cycle detection.
///
/// The database only blocks a subject requiring ITSELF, so a two-step loop
/// passes every constraint and then hangs the prerequisite tree forever. These
/// tests are the guarantee that the write paths catch it first.
/// </summary>
public class PrerequisiteGraphTests
{
    [Fact]
    public void WouldCreateCycle_rejects_a_subject_requiring_itself()
        => PrerequisiteGraph.WouldCreateCycle([], courseId: 1, requiredCourseId: 1).Should().BeTrue();

    [Fact]
    public void WouldCreateCycle_allows_an_edge_into_an_empty_graph()
        => PrerequisiteGraph.WouldCreateCycle([], courseId: 2, requiredCourseId: 1).Should().BeFalse();

    /// <summary>A requires B; adding "B requires A" closes the loop.</summary>
    [Fact]
    public void WouldCreateCycle_detects_a_two_step_loop()
    {
        var edges = new[] { (CourseId: 2, RequiredCourseId: 1) };

        PrerequisiteGraph.WouldCreateCycle(edges, courseId: 1, requiredCourseId: 2).Should().BeTrue();
    }

    /// <summary>The case the database cannot see at all: A -&gt; B -&gt; C -&gt; A.</summary>
    [Fact]
    public void WouldCreateCycle_detects_a_longer_loop()
    {
        var edges = new[]
        {
            (CourseId: 2, RequiredCourseId: 1),
            (CourseId: 3, RequiredCourseId: 2)
        };

        PrerequisiteGraph.WouldCreateCycle(edges, courseId: 1, requiredCourseId: 3).Should().BeTrue();
    }

    /// <summary>A diamond is not a cycle, and must not be rejected as one.</summary>
    [Fact]
    public void WouldCreateCycle_allows_a_diamond()
    {
        var edges = new[]
        {
            (CourseId: 2, RequiredCourseId: 1),
            (CourseId: 3, RequiredCourseId: 1)
        };

        PrerequisiteGraph.WouldCreateCycle(edges, courseId: 4, requiredCourseId: 2).Should().BeFalse();
        PrerequisiteGraph.WouldCreateCycle(edges, courseId: 4, requiredCourseId: 3).Should().BeFalse();
    }

    /// <summary>
    /// Bad data may already contain a cycle - a row inserted before this check
    /// existed. The walk must terminate rather than hang.
    /// </summary>
    [Fact]
    public void WouldCreateCycle_terminates_when_the_existing_graph_already_loops()
    {
        var edges = new[]
        {
            (CourseId: 1, RequiredCourseId: 2),
            (CourseId: 2, RequiredCourseId: 1)
        };

        var act = () => PrerequisiteGraph.WouldCreateCycle(edges, courseId: 3, requiredCourseId: 1);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void GetAllRequirements_walks_the_whole_chain()
    {
        // PRN222 -> PRN212 -> PRO192 -> PRF192
        var edges = new[]
        {
            (CourseId: 4, RequiredCourseId: 3),
            (CourseId: 3, RequiredCourseId: 2),
            (CourseId: 2, RequiredCourseId: 1)
        };

        PrerequisiteGraph.GetAllRequirements(edges, courseId: 4).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void GetAllRequirements_never_includes_the_subject_itself()
    {
        var edges = new[]
        {
            (CourseId: 1, RequiredCourseId: 2),
            (CourseId: 2, RequiredCourseId: 1)
        };

        PrerequisiteGraph.GetAllRequirements(edges, courseId: 1).Should().NotContain(1);
    }

    [Fact]
    public void GetAllRequirements_returns_nothing_for_a_subject_with_no_prerequisites()
        => PrerequisiteGraph.GetAllRequirements([], courseId: 1).Should().BeEmpty();
}
