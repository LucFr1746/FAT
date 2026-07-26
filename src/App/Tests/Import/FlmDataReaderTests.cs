using FluentAssertions;
using Services.Import;
using Tests.TestSupport;

namespace Tests.Import;

/// <summary>
/// Reads the REAL committed FLM export.
///
/// Deliberately not a hand-made fixture: the whole point of these tests is that
/// the readers survive the actual file, with its Vietnamese text, multi-line
/// quoted descriptions and inconsistent capitalisation. A tidy fixture would
/// pass while the real import failed.
/// </summary>
public class FlmDataReaderTests
{
    /// <summary>The six programmes in the FLM export.</summary>
    private const int ExpectedMajorCount = 6;

    /// <summary>Distinct subject codes across all six programmes.</summary>
    private const int ExpectedSubjectCount = 135;

    [SkippableFact]
    public async Task Xlsx_reader_loads_every_programme_and_subject()
    {
        var path = RepositoryPaths.FlmWorkbook;
        Skip.If(path is null || !File.Exists(path), "db/data/flm_chuong_trinh_hoc.xlsx is not available.");

        var data = await new XlsxFlmDataReader().ReadAsync(path!);

        data.Curricula.Should().HaveCount(ExpectedMajorCount);
        data.Subjects.Select(s => s.SubjectCode).Distinct()
            .Should().HaveCountGreaterThanOrEqualTo(ExpectedSubjectCount);
        data.Assessments.Should().NotBeEmpty();
        data.Materials.Should().NotBeEmpty();
        data.Schedules.Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task Json_reader_loads_every_programme_and_subject()
    {
        var path = RepositoryPaths.FlmJsonFolder;
        Skip.If(path is null || !Directory.Exists(path), "db/data/json is not available.");

        var data = await new JsonFlmDataReader().ReadAsync(path!);

        data.Curricula.Should().HaveCount(ExpectedMajorCount);
        data.Subjects.Select(s => s.SubjectCode).Distinct()
            .Should().HaveCountGreaterThanOrEqualTo(ExpectedSubjectCount);
        data.Assessments.Should().NotBeEmpty();
        data.Materials.Should().NotBeEmpty();
        data.Schedules.Should().NotBeEmpty();
    }

    /// <summary>
    /// The two sources are offered as interchangeable, so they must agree on the
    /// catalog. They are not byte-identical - the workbook is a curated view and
    /// drops retired codes such as DBI202-OLD - so a small, bounded difference is
    /// expected. A large one means a reader is misreading its columns, which is
    /// what this guards against: an earlier version of the JSON reader ignored
    /// combos.json and silently lost 48 elective subjects.
    /// </summary>
    [SkippableFact]
    public async Task Xlsx_and_json_readers_agree_on_the_core_catalog()
    {
        Skip.If(RepositoryPaths.FlmWorkbook is null || !File.Exists(RepositoryPaths.FlmWorkbook),
            "FLM data is not available.");

        var fromXlsx = await new XlsxFlmDataReader().ReadAsync(RepositoryPaths.FlmWorkbook!);
        var fromJson = await new JsonFlmDataReader().ReadAsync(RepositoryPaths.FlmJsonFolder!);

        fromXlsx.Curricula.Select(c => c.MajorCode)
            .Should().BeEquivalentTo(fromJson.Curricula.Select(c => c.MajorCode));

        var xlsxSubjects = fromXlsx.Subjects.Select(s => s.SubjectCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var jsonSubjects = fromJson.Subjects.Select(s => s.SubjectCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        xlsxSubjects.Intersect(jsonSubjects).Should().HaveCountGreaterThanOrEqualTo(ExpectedSubjectCount - 5);
        jsonSubjects.Except(xlsxSubjects).Should().HaveCountLessThanOrEqualTo(
            5, "only a handful of retired codes should differ between the two sources");
    }

    /// <summary>
    /// Kỳ 0 is the case a "term starts at 1" assumption silently drops: OTP101
    /// would vanish from every curriculum without anyone noticing.
    /// </summary>
    [SkippableFact]
    public async Task Reader_keeps_term_zero_subjects()
    {
        Skip.If(RepositoryPaths.FlmWorkbook is null || !File.Exists(RepositoryPaths.FlmWorkbook),
            "FLM data is not available.");

        var data = await new XlsxFlmDataReader().ReadAsync(RepositoryPaths.FlmWorkbook!);

        data.Subjects.Where(s => s.TermNo == 0).Should().NotBeEmpty();
        data.Subjects.Should().Contain(s => s.SubjectCode == "OTP101" && s.TermNo == 0);
    }

    /// <summary>
    /// Physical education and the orientation block carry credits but must not
    /// enter the GPA. If this flag were lost, every affected student's GPA would
    /// shift.
    /// </summary>
    [SkippableFact]
    public async Task Reader_marks_non_gpa_subjects()
    {
        Skip.If(RepositoryPaths.FlmWorkbook is null || !File.Exists(RepositoryPaths.FlmWorkbook),
            "FLM data is not available.");

        var data = await new XlsxFlmDataReader().ReadAsync(RepositoryPaths.FlmWorkbook!);

        data.Subjects.Should().Contain(s => !s.CountsTowardGpa);
        data.Subjects.Should().Contain(s => s.CountsTowardGpa);
    }

    /// <summary>
    /// The weights are the input to every final score, so a reader that
    /// misparses "30.0%" would corrupt grading across the board. The FLM data is
    /// known to balance, so anything that does not is a parsing fault.
    /// </summary>
    [SkippableFact]
    public async Task Grade_structure_weights_sum_to_one_hundred_percent()
    {
        Skip.If(RepositoryPaths.FlmWorkbook is null || !File.Exists(RepositoryPaths.FlmWorkbook),
            "FLM data is not available.");

        var data = await new XlsxFlmDataReader().ReadAsync(RepositoryPaths.FlmWorkbook!);

        var unbalanced = data.Assessments
            .Where(a => !a.IsSubComponent)
            .GroupBy(a => a.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Subject = g.Key, Total = g.Sum(a => a.WeightPercent) })
            .Where(x => Math.Abs(x.Total - 100m) > 0.5m)
            .ToList();

        unbalanced.Should().BeEmpty();
    }
}
