using Domain.Constants;
using FluentAssertions;

namespace Tests.DomainRules;

public class CatalogRulesTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.9999)]
    [InlineData(1.0001)]
    public void IsWeightTotalValid_accepts_totals_within_the_tolerance(decimal total)
        => CatalogRules.IsWeightTotalValid(total).Should().BeTrue();

    [Theory]
    [InlineData(0.9)]
    [InlineData(1.1)]
    [InlineData(0.0)]
    public void IsWeightTotalValid_rejects_totals_outside_the_tolerance(decimal total)
        => CatalogRules.IsWeightTotalValid(total).Should().BeFalse();

    /// <summary>
    /// A 33.3/33.3/33.4 split is a real FLM structure. Demanding an exact 1.0000
    /// would reject it, which is the whole reason the tolerance exists.
    /// </summary>
    [Fact]
    public void IsWeightTotalValid_accepts_a_three_way_split()
        => CatalogRules.IsWeightTotalValid(0.3333m + 0.3333m + 0.3334m).Should().BeTrue();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(15, 8)]
    [InlineData(30, 15)]
    public void GetWeekNo_maps_two_sessions_to_each_week(int sessionNo, int expectedWeek)
        => CatalogRules.GetWeekNo(sessionNo).Should().Be(expectedWeek);

    /// <summary>A nonsensical session number must not produce week 0 or a negative.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GetWeekNo_falls_back_to_week_one_for_invalid_input(int sessionNo)
        => CatalogRules.GetWeekNo(sessionNo).Should().Be(1);

    [Fact]
    public void GetTermName_labels_term_zero_as_the_orientation_block()
    {
        CatalogRules.GetTermName(0).Should().Contain("Kỳ 0");
        CatalogRules.GetTermName(3).Should().Be("Kỳ 3");
    }
}
