using FAT.Domain.Constants;
using FAT.Domain.Enums;
using FluentAssertions;

namespace FAT.Tests.DomainRules;

/// <summary>
/// The retake penalty. Boundaries get their own cases because that is exactly
/// where an off-by-one hides: writing &gt; instead of &gt;= silently spares
/// every student with exactly three retakes.
/// </summary>
public class GraduationRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(10, 1)]
    public void GetDemotionSteps_costs_a_rank_from_three_retaken_subjects(int retakes, int expectedSteps)
        => GraduationRules.GetDemotionSteps(retakes).Should().Be(expectedSteps);

    /// <summary>Exactly at the threshold - the case a strict &gt; would miss.</summary>
    [Fact]
    public void GetDemotionSteps_treats_exactly_three_retakes_as_over_the_threshold()
        => GraduationRules.GetDemotionSteps(GraduationRules.RetakeDemotionThreshold).Should().Be(1);

    [Theory]
    [InlineData(DegreeClassification.Excellent, DegreeClassification.VeryGood)]
    [InlineData(DegreeClassification.VeryGood, DegreeClassification.Good)]
    [InlineData(DegreeClassification.Good, DegreeClassification.FairGood)]
    [InlineData(DegreeClassification.FairGood, DegreeClassification.Average)]
    [InlineData(DegreeClassification.Average, DegreeClassification.NotQualified)]
    public void Demote_moves_one_rank_down_the_ladder(
        DegreeClassification from, DegreeClassification expected)
        => GraduationRules.Demote(from, 1).Should().Be(expected);

    /// <summary>The bottom of the ladder is a floor, not a wrap-around.</summary>
    [Fact]
    public void Demote_never_falls_below_not_qualified()
        => GraduationRules.Demote(DegreeClassification.NotQualified, 5)
            .Should().Be(DegreeClassification.NotQualified);

    [Fact]
    public void Demote_returns_the_original_when_there_is_nothing_to_deduct()
        => GraduationRules.Demote(DegreeClassification.Excellent, 0)
            .Should().Be(DegreeClassification.Excellent);

    [Fact]
    public void ClassifyWithRetakes_leaves_the_classification_alone_below_the_threshold()
        => GraduationRules.ClassifyWithRetakes(8.5m, 2).Should().Be(DegreeClassification.VeryGood);

    [Fact]
    public void ClassifyWithRetakes_demotes_at_the_threshold()
        => GraduationRules.ClassifyWithRetakes(8.5m, 3).Should().Be(DegreeClassification.Good);

    /// <summary>
    /// The GPA boundary AND the penalty together: 8.0 is Very Good on its own,
    /// so a demotion must land exactly on Good rather than skipping a rank.
    /// </summary>
    [Fact]
    public void ClassifyWithRetakes_applies_the_penalty_from_the_correct_starting_rank()
    {
        AcademicRules.ClassifyGpa(8.0m).Should().Be(DegreeClassification.VeryGood);
        GraduationRules.ClassifyWithRetakes(8.0m, 3).Should().Be(DegreeClassification.Good);
    }

    [Fact]
    public void DescribeDemotion_explains_the_penalty_only_when_it_applies()
    {
        GraduationRules.DescribeDemotion(2).Should().BeNull();
        GraduationRules.DescribeDemotion(3).Should().NotBeNull().And.Contain("3");
    }
}
