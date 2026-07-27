using System.Text.RegularExpressions;

namespace Services.Import;

/// <summary>
/// Turns FLM's free-text prerequisite column into structured requirements.
///
/// The column is written by humans and comes in five recognisable shapes:
///
///   "ECO111"                          one subject
///   "Passed VOV114"                   one subject, with noise in front
///   "MLN111, MLN122"                  BOTH are required          (AND)
///   "MGT101 or MGT103 or MKG101"      ANY ONE is enough          (OR)
///   "MGT101/MGT103/MKG101"            same thing, slash-separated (OR)
///
/// plus a sixth that is not machine-readable at all:
///
///   "Sinh viên đạt 90% tổng số tín chỉ trước kỳ OJT"
///
/// The last case is why <see cref="ParseResult.IsFullyParsed"/> exists. A rule
/// nobody can express as course ids must NOT be silently dropped - the caller
/// keeps the original sentence in Course.PrerequisiteText so a human can still
/// see it.
///
/// Pure and static: no database, no state, so every shape above is covered by a
/// plain unit test.
/// </summary>
public static partial class PrerequisiteTextParser
{
    /// <summary>
    /// An FPT subject code: 2-4 capitals, 3 digits, and sometimes one trailing
    /// lower-case letter (PMG201c, HRM202c, IBS301m).
    ///
    /// Anchored with \b so that "90%" and years never match.
    /// </summary>
    [GeneratedRegex(@"\b[A-Z]{2,4}\d{3}[a-z]?\b", RegexOptions.CultureInvariant)]
    private static partial Regex SubjectCodeRegex();

    /// <summary>Splits on the separators that mean AND: comma, semicolon, "and", "và", "&amp;".</summary>
    [GeneratedRegex(@"\s*(?:,|;|\band\b|\bvà\b|&)\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AndSeparatorRegex();

    /// <summary>Splits on the separators that mean OR: "or", "hoặc", slash, pipe.</summary>
    [GeneratedRegex(@"\s*(?:\bor\b|\bhoặc\b|/|\|)\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrSeparatorRegex();

    /// <summary>Values that mean "no prerequisite" rather than an actual requirement.</summary>
    private static readonly string[] EmptyMarkers =
        ["none", "n/a", "na", "không", "khong", "không có", "-", "không None"];

    /// <summary>
    /// One requirement. A single-element <see cref="Alternatives"/> list is a
    /// plain requirement; several elements mean "any one of these".
    /// </summary>
    public sealed record RequirementGroup(IReadOnlyList<string> Alternatives)
    {
        public bool IsChoice => Alternatives.Count > 1;
    }

    /// <summary>Outcome of parsing one cell.</summary>
    public sealed record ParseResult(
        IReadOnlyList<RequirementGroup> Groups,
        string? OriginalText,
        bool IsFullyParsed)
    {
        public static ParseResult None { get; } = new([], null, true);

        /// <summary>Every code mentioned, flattened - handy for existence checks.</summary>
        public IReadOnlyList<string> AllCodes =>
            Groups.SelectMany(g => g.Alternatives).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public bool HasRequirements => Groups.Count > 0;
    }

    /// <summary>
    /// Parses one prerequisite cell.
    ///
    /// The result is AND-of-ORs: every group must be satisfied, and a group is
    /// satisfied by any one of its alternatives.
    /// </summary>
    public static ParseResult Parse(string? text)
    {
        var cleaned = FlmValueParser.Clean(text);
        if (cleaned is null || IsEmptyMarker(cleaned))
        {
            return ParseResult.None;
        }

        var groups = new List<RequirementGroup>();

        // AND first, then OR inside each part. That order matters:
        // "A, B or C" means "A" AND "either B or C", which splitting on OR
        // first would flatten into a single wrong choice of three.
        foreach (var andPart in AndSeparatorRegex().Split(cleaned))
        {
            if (string.IsNullOrWhiteSpace(andPart))
            {
                continue;
            }

            var alternatives = OrSeparatorRegex()
                .Split(andPart)
                .SelectMany(orPart => SubjectCodeRegex().Matches(orPart).Select(m => m.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (alternatives.Count > 0)
            {
                groups.Add(new RequirementGroup(alternatives));
            }
        }

        // "Fully parsed" compares code counts rather than checking for leftover
        // words: "Passed VOV114" is fully understood even though "Passed" is
        // not a code, whereas a sentence with no codes at all is not.
        var codesFound = groups.Sum(g => g.Alternatives.Count);
        var codesInText = SubjectCodeRegex().Matches(cleaned).Count;
        var isFullyParsed = codesFound > 0 && codesFound >= codesInText;

        return new ParseResult(groups, cleaned, isFullyParsed);
    }

    private static bool IsEmptyMarker(string value)
        => EmptyMarkers.Any(marker => string.Equals(marker, value, StringComparison.OrdinalIgnoreCase));
}
