using Services.Import;
using FluentAssertions;

namespace Tests.Import;

/// <summary>
/// Covers every shape the FLM prerequisite column actually takes. The examples
/// are copied verbatim from db/data/csv/subjects.csv rather than invented, so a
/// passing test means the real file parses.
/// </summary>
public class PrerequisiteTextParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("Không")]
    [InlineData("-")]
    public void Parse_returns_no_requirements_for_empty_markers(string? text)
    {
        var result = PrerequisiteTextParser.Parse(text);

        result.HasRequirements.Should().BeFalse();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_reads_a_single_subject_code()
    {
        var result = PrerequisiteTextParser.Parse("ECO111");

        result.Groups.Should().ContainSingle();
        result.Groups[0].Alternatives.Should().ContainSingle().Which.Should().Be("ECO111");
        result.Groups[0].IsChoice.Should().BeFalse();
        result.IsFullyParsed.Should().BeTrue();
    }

    /// <summary>"Passed X" is noise around a code, not an unparsed requirement.</summary>
    [Fact]
    public void Parse_ignores_leading_words_around_a_code()
    {
        var result = PrerequisiteTextParser.Parse("Passed VOV114");

        result.AllCodes.Should().ContainSingle().Which.Should().Be("VOV114");
        result.IsFullyParsed.Should().BeTrue();
    }

    /// <summary>Comma means AND: both subjects are required.</summary>
    [Fact]
    public void Parse_treats_a_comma_as_and()
    {
        var result = PrerequisiteTextParser.Parse("MLN111, MLN122");

        result.Groups.Should().HaveCount(2);
        result.Groups.Should().OnlyContain(g => !g.IsChoice);
        result.AllCodes.Should().BeEquivalentTo(["MLN111", "MLN122"]);
    }

    /// <summary>"or" means one group with several alternatives.</summary>
    [Fact]
    public void Parse_treats_or_as_a_single_choice_group()
    {
        var result = PrerequisiteTextParser.Parse("MGT101 or MGT103 or MKG101");

        result.Groups.Should().ContainSingle();
        result.Groups[0].IsChoice.Should().BeTrue();
        result.Groups[0].Alternatives.Should().BeEquivalentTo(["MGT101", "MGT103", "MKG101"]);
    }

    /// <summary>A slash is the same idea written differently.</summary>
    [Fact]
    public void Parse_treats_a_slash_as_a_choice_group()
    {
        var result = PrerequisiteTextParser.Parse("MGT101/MGT103/MKG101");

        result.Groups.Should().ContainSingle();
        result.Groups[0].IsChoice.Should().BeTrue();
        result.Groups[0].Alternatives.Should().HaveCount(3);
    }

    /// <summary>
    /// The case that makes the AND-before-OR order matter: splitting on OR first
    /// would collapse this into one wrong choice of three.
    /// </summary>
    [Fact]
    public void Parse_keeps_and_outside_or_when_both_appear()
    {
        var result = PrerequisiteTextParser.Parse("ACC101, MGT101 or MKG101");

        result.Groups.Should().HaveCount(2);
        result.Groups.Should().ContainSingle(g => !g.IsChoice && g.Alternatives[0] == "ACC101");
        result.Groups.Should().ContainSingle(g => g.IsChoice && g.Alternatives.Count == 2);
    }

    /// <summary>
    /// Prose with no codes must report itself as unparsed so the caller keeps the
    /// original sentence instead of silently dropping the rule.
    /// </summary>
    [Fact]
    public void Parse_flags_prose_that_contains_no_subject_codes()
    {
        var result = PrerequisiteTextParser.Parse(
            "Sinh viên đạt 90% tổng số tín chỉ trước kỳ OJT (không tính GDTC)");

        result.HasRequirements.Should().BeFalse();
        result.OriginalText.Should().NotBeNull();
    }

    /// <summary>Trailing lower-case letters are part of the code (PMG201c, IBS301m).</summary>
    [Theory]
    [InlineData("PMG201c", "PMG201c")]
    [InlineData("IBS301m", "IBS301m")]
    [InlineData("HRM202c", "HRM202c")]
    public void Parse_reads_codes_with_a_trailing_letter(string text, string expected)
    {
        PrerequisiteTextParser.Parse(text).AllCodes.Should().ContainSingle().Which.Should().Be(expected);
    }

    /// <summary>Percentages and years are not subject codes.</summary>
    [Fact]
    public void Parse_does_not_mistake_numbers_for_codes()
    {
        PrerequisiteTextParser.Parse("Pass 80% of total credits in 2024").AllCodes.Should().BeEmpty();
    }

    /// <summary>A repeated code is one requirement, not two.</summary>
    [Fact]
    public void Parse_deduplicates_repeated_codes()
    {
        PrerequisiteTextParser.Parse("ECO111 or ECO111").AllCodes.Should().ContainSingle();
    }

    /// <summary>
    /// The real EXE401 entry: a numbered list mixing one code with two prose
    /// conditions. The code must be found and the result marked partial.
    /// </summary>
    [Fact]
    public void Parse_extracts_codes_from_a_mixed_prose_list()
    {
        var result = PrerequisiteTextParser.Parse(
            "1. EXE201 2. Pass On-the-job training 3. Pass 80% of total credits");

        result.AllCodes.Should().Contain("EXE201");
    }
}
