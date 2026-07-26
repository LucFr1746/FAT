using Data;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Tests.Services;

/// <summary>
/// Assign Subject to Major, and Curriculum Management.
///
/// The recurring assertion is that Major.RequiredCredits equals the curriculum
/// total after EVERY operation: it is the denominator of the graduation
/// percentage, and drift there is wrong for every student in the programme with
/// nothing in the UI to explain it.
/// </summary>
public class CurriculumAdminServiceTests
{
    private static CurriculumAdminService CreateService(FAT_DBContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    [Fact]
    public async Task AssignAsync_adds_the_subject_and_syncs_the_credit_total()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192", credits: 3);

        await CreateService(db).AssignAsync(major.MajorId, course.CourseId, termNo: 1);

        (await db.CurriculumItems.CountAsync()).Should().Be(1);
        (await db.Majors.SingleAsync()).RequiredCredits.Should().Be(3);
    }

    [Fact]
    public async Task AssignAsync_rejects_a_subject_already_in_the_programme()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");
        db.AddCurriculumItem(major.MajorId, course.CourseId, 1);

        var act = () => CreateService(db).AssignAsync(major.MajorId, course.CourseId, 2);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã có trong chương trình*");
    }

    /// <summary>Curriculum.TermNo is a foreign key; an undeclared kỳ must fail with words, not a constraint error.</summary>
    [Fact]
    public async Task AssignAsync_rejects_a_term_that_has_not_been_declared()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).AssignAsync(major.MajorId, course.CourseId, termNo: 11);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*chưa được khai báo*");
    }

    [Fact]
    public async Task AssignAsync_accepts_term_zero()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("OTP101", credits: 0);

        await CreateService(db).AssignAsync(major.MajorId, course.CourseId, termNo: 0);

        (await db.CurriculumItems.SingleAsync()).TermNo.Should().Be(0);
    }

    /// <summary>
    /// One accidental selection must not abandon the whole batch: the duplicate
    /// is reported and the rest still land.
    /// </summary>
    [Fact]
    public async Task BulkAssignAsync_skips_duplicates_and_keeps_going()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var already = db.AddCourse("PRF192", credits: 3);
        var newA = db.AddCourse("MAE101", credits: 3);
        var newB = db.AddCourse("CEA201", credits: 3);
        db.AddCurriculumItem(major.MajorId, already.CourseId, 1);

        var result = await CreateService(db).BulkAssignAsync(
            major.MajorId, [already.CourseId, newA.CourseId, newB.CourseId], termNo: 1);

        result.Succeeded.Should().Be(2);
        result.Skipped.Should().Be(1);
        result.Messages.Should().Contain(m => m.Contains("PRF192"));
        (await db.Majors.SingleAsync()).RequiredCredits.Should().Be(9);
    }

    [Fact]
    public async Task BulkAssignAsync_reports_unknown_subject_ids()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");

        var result = await CreateService(db).BulkAssignAsync(major.MajorId, [9999], termNo: 1);

        result.Succeeded.Should().Be(0);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task BulkAssignAsync_handles_an_empty_selection()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");

        var result = await CreateService(db).BulkAssignAsync(major.MajorId, [], termNo: 1);

        result.Succeeded.Should().Be(0);
        result.Messages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_deletes_the_link_and_syncs_the_credit_total()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var keep = db.AddCourse("PRF192", credits: 3);
        var drop = db.AddCourse("MAE101", credits: 4);
        db.AddCurriculumItem(major.MajorId, keep.CourseId, 1);
        var toRemove = db.AddCurriculumItem(major.MajorId, drop.CourseId, 1);

        await CreateService(db).RemoveAsync(toRemove.CurriculumId);

        (await db.CurriculumItems.CountAsync()).Should().Be(1);
        (await db.Majors.SingleAsync()).RequiredCredits.Should().Be(3);
    }

    /// <summary>
    /// CK_Major_Credit demands RequiredCredits &gt; 0, so emptying a curriculum
    /// must fall back to 1 rather than write a 0 that the database rejects.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_keeps_the_credit_total_positive_when_the_curriculum_empties()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192", credits: 3);
        var item = db.AddCurriculumItem(major.MajorId, course.CourseId, 1);

        await CreateService(db).RemoveAsync(item.CurriculumId);

        (await db.Majors.SingleAsync()).RequiredCredits.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkRemoveAsync_syncs_every_affected_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var ai = db.AddMajor("AI");
        var shared = db.AddCourse("MAD101", credits: 3);
        var seOnly = db.AddCourse("PRF192", credits: 3);

        var seShared = db.AddCurriculumItem(se.MajorId, shared.CourseId, 1);
        var aiShared = db.AddCurriculumItem(ai.MajorId, shared.CourseId, 2);
        db.AddCurriculumItem(se.MajorId, seOnly.CourseId, 1);

        await CreateService(db).BulkRemoveAsync([seShared.CurriculumId, aiShared.CurriculumId]);

        (await db.Majors.SingleAsync(m => m.MajorId == se.MajorId)).RequiredCredits.Should().Be(3);
        // An emptied curriculum falls back to 1 to satisfy CK_Major_Credit.
        (await db.Majors.SingleAsync(m => m.MajorId == ai.MajorId)).RequiredCredits.Should().Be(1);
    }

    [Fact]
    public async Task ReorderAsync_rewrites_the_positions_within_a_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var a = db.AddCurriculumItem(major.MajorId, db.AddCourse("AAA101").CourseId, 1, displayOrder: 0);
        var b = db.AddCurriculumItem(major.MajorId, db.AddCourse("BBB101").CourseId, 1, displayOrder: 1);
        var c = db.AddCurriculumItem(major.MajorId, db.AddCourse("CCC101").CourseId, 1, displayOrder: 2);

        await CreateService(db).ReorderAsync(
            major.MajorId, 1, [c.CurriculumId, a.CurriculumId, b.CurriculumId]);

        (await db.CurriculumItems.SingleAsync(i => i.CurriculumId == c.CurriculumId)).DisplayOrder.Should().Be(0);
        (await db.CurriculumItems.SingleAsync(i => i.CurriculumId == a.CurriculumId)).DisplayOrder.Should().Be(1);
        (await db.CurriculumItems.SingleAsync(i => i.CurriculumId == b.CurriculumId)).DisplayOrder.Should().Be(2);
    }

    /// <summary>
    /// A partial list would renumber only the mentioned items and leave the rest
    /// on stale positions, producing duplicate orders.
    /// </summary>
    [Fact]
    public async Task ReorderAsync_rejects_an_incomplete_ordering()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var a = db.AddCurriculumItem(major.MajorId, db.AddCourse("AAA101").CourseId, 1);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("BBB101").CourseId, 1);

        var act = () => CreateService(db).ReorderAsync(major.MajorId, 1, [a.CurriculumId]);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đầy đủ*");
    }

    [Fact]
    public async Task UpdateItemAsync_moves_a_subject_to_the_end_of_its_new_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("AAA101").CourseId, 2, displayOrder: 0);
        var moving = db.AddCurriculumItem(major.MajorId, db.AddCourse("BBB101").CourseId, 1, displayOrder: 0);

        await CreateService(db).UpdateItemAsync(moving.CurriculumId, termNo: 2, isMandatory: true);

        var moved = await db.CurriculumItems.SingleAsync(i => i.CurriculumId == moving.CurriculumId);
        moved.TermNo.Should().Be(2);
        moved.DisplayOrder.Should().Be(1, "it takes the next free slot rather than colliding at 0");
    }

    [Fact]
    public async Task GetUnassignedCoursesAsync_excludes_subjects_already_in_the_programme()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var assigned = db.AddCourse("PRF192");
        db.AddCourse("MAE101");
        db.AddCurriculumItem(major.MajorId, assigned.CourseId, 1);

        var results = await CreateService(db).GetUnassignedCoursesAsync(major.MajorId);

        results.Should().ContainSingle().Which.CourseCode.Should().Be("MAE101");
    }

    [Fact]
    public async Task GetByMajorAsync_orders_by_term_then_position()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("TERM2B").CourseId, 2, displayOrder: 1);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("TERM2A").CourseId, 2, displayOrder: 0);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("TERM1A").CourseId, 1, displayOrder: 0);

        var results = await CreateService(db).GetByMajorAsync(major.MajorId);

        results.Select(r => r.CourseCode).Should().ContainInOrder("TERM1A", "TERM2A", "TERM2B");
    }

    [Fact]
    public async Task GetMajorCodesByCourseIdsAsync_groups_by_course_and_ignores_unassigned_ids()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var ai = db.AddMajor("AI");
        var shared = db.AddCourse("PRF192");
        var seOnly = db.AddCourse("SWE201c");
        var unassigned = db.AddCourse("CSD201");
        db.AddCurriculumItem(se.MajorId, shared.CourseId, 1);
        db.AddCurriculumItem(ai.MajorId, shared.CourseId, 1);
        db.AddCurriculumItem(se.MajorId, seOnly.CourseId, 2);

        var result = await CreateService(db).GetMajorCodesByCourseIdsAsync(
            [shared.CourseId, seOnly.CourseId, unassigned.CourseId]);

        result[shared.CourseId].Should().BeEquivalentTo(["AI", "SE"]);
        result[seOnly.CourseId].Should().BeEquivalentTo(["SE"]);
        result.Should().NotContainKey(unassigned.CourseId);
    }

    [Fact]
    public async Task GetMajorCodesByCourseIdsAsync_returns_empty_for_an_empty_request()
    {
        using var db = TestDb.CreateWithReferenceData();

        var result = await CreateService(db).GetMajorCodesByCourseIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportToCsvAsync_writes_a_header_and_one_row_per_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE", "Software Engineering");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("PRF192").CourseId, 1);

        var csv = await CreateService(db).ExportToCsvAsync(major.MajorId);

        csv.Should().Contain("MaNganh").And.Contain("PRF192");
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(2);
    }

    [Fact]
    public async Task Curriculum_writes_require_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, asAdmin: false).AssignAsync(major.MajorId, course.CourseId, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
