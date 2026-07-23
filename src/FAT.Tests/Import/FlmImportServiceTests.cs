using FAT.Data;
using FAT.Services.Dtos;
using FAT.Services.Implementations;
using FAT.Services.Import;
using FAT.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Import;

/// <summary>
/// The import's contract is idempotency, so most of these tests run the same
/// import twice and assert the second run creates nothing.
/// </summary>
public class FlmImportServiceTests
{
    /// <summary>
    /// A reader that returns a fixed data set, so the upsert logic can be tested
    /// without depending on a file on disk.
    /// </summary>
    private sealed class StubReader(FlmDataSet data) : IFlmDataReader
    {
        public string SourceName => "Stub";
        public bool CanRead(string path) => true;
        public Task<FlmDataSet> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(data);
    }

    private static FlmImportService CreateService(FatDbContext db, FlmDataSet data, bool asAdmin = true)
        => new(db,
            asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1),
            [new StubReader(data)]);

    /// <summary>
    /// Two programmes sharing one subject, with a grade structure, a reading, a
    /// scheduled test and a prerequisite - one of each thing the import writes.
    /// </summary>
    private static FlmDataSet SampleData() => new(
        Curricula:
        [
            new FlmCurriculumRow("BIT_SE", "Software Engineering"),
            new FlmCurriculumRow("BIT_AI", "Artificial Intelligence")
        ],
        Subjects:
        [
            new FlmSubjectRow("BIT_SE", "PRF192", "Programming Fundamentals", 1, 3, true, null, "Intro", "111", null),
            new FlmSubjectRow("BIT_SE", "PRO192", "Object-Oriented Programming", 2, 3, true, "PRF192", null, "112", null),
            new FlmSubjectRow("BIT_SE", "VOV114", "Vovinam 1", 0, 2, false, "None", null, "113", null),
            // The same subject in a DIFFERENT kỳ - the case that forces the term
            // onto the curriculum link rather than onto the subject.
            new FlmSubjectRow("BIT_AI", "PRF192", "Programming Fundamentals", 2, 3, true, null, "Intro", "111", null)
        ],
        Assessments:
        [
            new FlmAssessmentRow("PRF192", "Assignment", "on-going", 40m, ">0", false, 0),
            new FlmAssessmentRow("PRF192", "Final exam", "Final exam", 60m, "4", false, 1),
            // A sub-component: excluded, or the total would read 130%.
            new FlmAssessmentRow("PRF192", "Assignment detail", "on-going", 30m, ">0", true, 2)
        ],
        Materials:
        [
            new FlmMaterialRow("PRF192", "C Programming Language", "https://example.com/k-r",
                "Kernighan", "Prentice Hall", "0131103628", null, 0)
        ],
        Schedules:
        [
            new FlmScheduleRow("PRF192", 15, "Progress test 1", "Offline"),
            new FlmScheduleRow("PRF192", 30, "Final exam", "Offline")
        ]);

    [Fact]
    public async Task ImportAsync_creates_the_whole_catalog_on_a_first_run()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, SampleData());

        var result = await service.ImportAsync("stub");

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        (await db.Majors.CountAsync()).Should().Be(2);
        (await db.Courses.CountAsync()).Should().Be(3);
        (await db.CurriculumItems.CountAsync()).Should().Be(4);
        (await db.Assessments.CountAsync()).Should().Be(2, "sub-components are not grade columns");
        (await db.SubjectMaterials.CountAsync()).Should().Be(1);
        (await db.AssessmentSchedules.CountAsync()).Should().Be(2);
        (await db.Prerequisites.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// THE contract. A second import must not duplicate anything - if it did,
    /// Major.RequiredCredits would double and every graduation percentage would
    /// halve.
    /// </summary>
    [Fact]
    public async Task ImportAsync_run_twice_creates_nothing_the_second_time()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, SampleData());

        await service.ImportAsync("stub");

        var countsAfterFirst = new
        {
            Majors = await db.Majors.CountAsync(),
            Courses = await db.Courses.CountAsync(),
            Curriculum = await db.CurriculumItems.CountAsync(),
            Assessments = await db.Assessments.CountAsync(),
            Materials = await db.SubjectMaterials.CountAsync(),
            Schedules = await db.AssessmentSchedules.CountAsync(),
            Prerequisites = await db.Prerequisites.CountAsync()
        };

        var second = await service.ImportAsync("stub");

        second.IsSuccess.Should().BeTrue();
        second.TotalCreated.Should().Be(0, "a re-import must update, never duplicate");

        (await db.Majors.CountAsync()).Should().Be(countsAfterFirst.Majors);
        (await db.Courses.CountAsync()).Should().Be(countsAfterFirst.Courses);
        (await db.CurriculumItems.CountAsync()).Should().Be(countsAfterFirst.Curriculum);
        (await db.Assessments.CountAsync()).Should().Be(countsAfterFirst.Assessments);
        (await db.SubjectMaterials.CountAsync()).Should().Be(countsAfterFirst.Materials);
        (await db.AssessmentSchedules.CountAsync()).Should().Be(countsAfterFirst.Schedules);
        (await db.Prerequisites.CountAsync()).Should().Be(countsAfterFirst.Prerequisites);
    }

    [Fact]
    public async Task ImportAsync_updates_a_changed_subject_instead_of_adding_another()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var renamed = SampleData() with
        {
            Subjects =
            [
                new FlmSubjectRow("BIT_SE", "PRF192", "Lập trình cơ bản", 1, 4, true, null, "Updated", "111", null)
            ]
        };

        var result = await CreateService(db, renamed).ImportAsync("stub");

        result.Subjects.Created.Should().Be(0);
        result.Subjects.Updated.Should().Be(1);

        var course = await db.Courses.SingleAsync(c => c.CourseCode == "PRF192");
        course.CourseName.Should().Be("Lập trình cơ bản");
        course.Credits.Should().Be(4);
    }

    /// <summary>
    /// The same subject placed in Kỳ 1 for one programme and Kỳ 2 for another -
    /// one Course, two curriculum links, each with its own term.
    /// </summary>
    [Fact]
    public async Task ImportAsync_places_one_subject_in_different_terms_per_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var course = await db.Courses.SingleAsync(c => c.CourseCode == "PRF192");
        var links = await db.CurriculumItems
            .Where(ci => ci.CourseId == course.CourseId)
            .Include(ci => ci.Major)
            .ToListAsync();

        links.Should().HaveCount(2);
        links.Single(l => l.Major!.MajorCode == "BIT_SE").TermNo.Should().Be(1);
        links.Single(l => l.Major!.MajorCode == "BIT_AI").TermNo.Should().Be(2);
    }

    /// <summary>Kỳ 0 is real data, not a bad row.</summary>
    [Fact]
    public async Task ImportAsync_keeps_term_zero_subjects()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var vovinam = await db.Courses.SingleAsync(c => c.CourseCode == "VOV114");
        var link = await db.CurriculumItems.SingleAsync(ci => ci.CourseId == vovinam.CourseId);

        link.TermNo.Should().Be(0);
        vovinam.CountsTowardGpa.Should().BeFalse("physical education carries credits but no GPA");
    }

    /// <summary>
    /// RequiredCredits is the denominator of the graduation percentage, so it has
    /// to equal the curriculum total after every import.
    /// </summary>
    [Fact]
    public async Task ImportAsync_syncs_required_credits_to_the_curriculum_total()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var major = await db.Majors.SingleAsync(m => m.MajorCode == "BIT_SE");
        var expected = await db.CurriculumItems
            .Where(ci => ci.MajorId == major.MajorId)
            .SumAsync(ci => ci.Course!.Credits);

        major.RequiredCredits.Should().Be(expected);
        major.TotalTerms.Should().Be(2, "the highest term in this curriculum is 2");
    }

    [Fact]
    public async Task ImportAsync_converts_percentage_weights_to_fractions()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var assessments = await db.Assessments.OrderBy(a => a.DisplayOrder).ToListAsync();

        assessments.Sum(a => a.Weight).Should().Be(1.00m);
        assessments.Single(a => a.Name == "Assignment").Weight.Should().Be(0.40m);
        assessments.Single(a => a.Name == "Final exam").MinScoreToPass.Should().Be(4m);
        assessments.Single(a => a.Name == "Assignment").MinScoreToPass
            .Should().BeNull("\">0\" means hand something in, not a minimum score");
    }

    [Fact]
    public async Task ImportAsync_records_the_prerequisite_relationship()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var pro192 = await db.Courses.SingleAsync(c => c.CourseCode == "PRO192");
        var prf192 = await db.Courses.SingleAsync(c => c.CourseCode == "PRF192");

        var prerequisite = await db.Prerequisites.SingleAsync();
        prerequisite.CourseId.Should().Be(pro192.CourseId);
        prerequisite.RequiredCourseId.Should().Be(prf192.CourseId);
        prerequisite.GroupNo.Should().Be(0, "a lone requirement is not a choice group");
    }

    [Fact]
    public async Task ImportAsync_stores_alternatives_as_one_choice_group()
    {
        using var db = TestDb.CreateWithReferenceData();

        var data = SampleData() with
        {
            Subjects =
            [
                new FlmSubjectRow("BIT_SE", "MGT101", "Management", 1, 3, true, null, null, null, null),
                new FlmSubjectRow("BIT_SE", "MKG101", "Marketing", 1, 3, true, null, null, null, null),
                new FlmSubjectRow("BIT_SE", "HRM202", "Human Resources", 3, 3, true,
                    "MGT101 or MKG101", null, null, null)
            ]
        };

        await CreateService(db, data).ImportAsync("stub");

        var hrm = await db.Courses.SingleAsync(c => c.CourseCode == "HRM202");
        var groups = await db.Prerequisites.Where(p => p.CourseId == hrm.CourseId).ToListAsync();

        groups.Should().HaveCount(2);
        groups.Select(g => g.GroupNo).Distinct().Should().ContainSingle()
            .Which.Should().BeGreaterThan(0, "alternatives share one positive group number");
    }

    /// <summary>A prose rule must survive as text rather than vanish.</summary>
    [Fact]
    public async Task ImportAsync_keeps_unparseable_prerequisites_as_text()
    {
        using var db = TestDb.CreateWithReferenceData();

        var data = SampleData() with
        {
            Subjects =
            [
                new FlmSubjectRow("BIT_SE", "OJT202", "On-the-job training", 8, 10, true,
                    "Sinh viên đạt 90% tổng số tín chỉ trước kỳ OJT", null, null, null)
            ]
        };

        var result = await CreateService(db, data).ImportAsync("stub");

        var course = await db.Courses.SingleAsync(c => c.CourseCode == "OJT202");
        course.PrerequisiteText.Should().Contain("90%");
        (await db.Prerequisites.CountAsync()).Should().Be(0);
        result.Warnings.Should().Contain(w => w.Contains("OJT202"));
    }

    /// <summary>
    /// A zero weight would violate CK_Assessment_Weight and abort the whole
    /// transaction, so the row is dropped with a warning instead.
    /// </summary>
    [Fact]
    public async Task ImportAsync_skips_grade_columns_with_an_invalid_weight()
    {
        using var db = TestDb.CreateWithReferenceData();

        var data = SampleData() with
        {
            Assessments = [new FlmAssessmentRow("PRF192", "Broken", "on-going", 0m, null, false, 0)]
        };

        var result = await CreateService(db, data).ImportAsync("stub");

        (await db.Assessments.CountAsync()).Should().Be(0);
        result.Assessments.Skipped.Should().Be(1);
        result.Warnings.Should().Contain(w => w.Contains("Broken"));
    }

    [Fact]
    public async Task ImportAsync_reports_a_weight_total_that_is_not_one_hundred_percent()
    {
        using var db = TestDb.CreateWithReferenceData();

        var data = SampleData() with
        {
            Assessments =
            [
                new FlmAssessmentRow("PRF192", "Assignment", "on-going", 40m, ">0", false, 0),
                new FlmAssessmentRow("PRF192", "Final exam", "Final exam", 40m, "4", false, 1)
            ]
        };

        var result = await CreateService(db, data).ImportAsync("stub");

        result.Warnings.Should().Contain(w => w.Contains("PRF192") && w.Contains("80"));
    }

    [Fact]
    public async Task ImportAsync_derives_the_week_from_the_session_number()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var schedule = await db.AssessmentSchedules.OrderBy(s => s.SessionNo).ToListAsync();

        // Two sessions a week: session 15 falls in week 8, session 30 in week 15.
        schedule[0].WeekNo.Should().Be(8);
        schedule[1].WeekNo.Should().Be(15);
        schedule.Should().OnlyContain(s => s.ExpectedDate == null, "FLM publishes no dates");
    }

    /// <summary>
    /// UpdateExisting = false is how an administrator protects hand-made
    /// corrections from a re-import.
    /// </summary>
    [Fact]
    public async Task ImportAsync_leaves_existing_rows_alone_when_updates_are_disabled()
    {
        using var db = TestDb.CreateWithReferenceData();
        await CreateService(db, SampleData()).ImportAsync("stub");

        var renamed = SampleData() with
        {
            Subjects = [new FlmSubjectRow("BIT_SE", "PRF192", "Renamed", 1, 9, true, null, null, null, null)]
        };

        await CreateService(db, renamed).ImportAsync("stub", new ImportOptions(UpdateExisting: false));

        var course = await db.Courses.SingleAsync(c => c.CourseCode == "PRF192");
        course.CourseName.Should().Be("Programming Fundamentals");
        course.Credits.Should().Be(3);
    }

    [Fact]
    public async Task ImportAsync_rejects_a_non_admin_caller()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, SampleData(), asAdmin: false);

        var act = () => service.ImportAsync("stub");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await db.Courses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_fails_cleanly_when_the_file_has_no_subjects()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, FlmDataSet.Empty);

        var result = await service.ImportAsync("stub");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task PreviewAsync_reports_the_contents_without_writing_anything()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, SampleData());

        var preview = await service.PreviewAsync("stub");

        preview.MajorCount.Should().Be(2);
        preview.SubjectCount.Should().Be(3);
        preview.CurriculumLinkCount.Should().Be(4);
        preview.AssessmentCount.Should().Be(2);

        (await db.Courses.CountAsync()).Should().Be(0, "a preview must not write");
    }

    /// <summary>
    /// The real workbook, through the real reader, into the real upsert logic.
    /// The narrow tests above use a stub; this one proves the whole path works on
    /// the actual file.
    /// </summary>
    [SkippableFact]
    public async Task ImportAsync_loads_the_real_workbook_and_stays_idempotent()
    {
        Skip.If(RepositoryPaths.FlmWorkbook is null || !File.Exists(RepositoryPaths.FlmWorkbook),
            "db/data/flm_chuong_trinh_hoc.xlsx is not available.");

        using var db = TestDb.CreateWithReferenceData();
        var service = new FlmImportService(db, TestCurrentUserContext.Admin());

        var first = await service.ImportAsync(RepositoryPaths.FlmWorkbook!);

        first.IsSuccess.Should().BeTrue();
        (await db.Majors.CountAsync()).Should().Be(6);
        (await db.Courses.CountAsync()).Should().BeGreaterThanOrEqualTo(135);
        (await db.Assessments.CountAsync()).Should().BeGreaterThan(0);

        var courseCount = await db.Courses.CountAsync();
        var curriculumCount = await db.CurriculumItems.CountAsync();

        var second = await service.ImportAsync(RepositoryPaths.FlmWorkbook!);

        second.TotalCreated.Should().Be(0);
        (await db.Courses.CountAsync()).Should().Be(courseCount);
        (await db.CurriculumItems.CountAsync()).Should().Be(curriculumCount);
    }
}
