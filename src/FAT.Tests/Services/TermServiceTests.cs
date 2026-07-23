using FAT.Data;
using FAT.Services.Dtos;
using FAT.Services.Implementations;
using FAT.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Services;

/// <summary>Manage Semester - the kỳ of the study path.</summary>
public class TermServiceTests
{
    private static TermService CreateService(FatDbContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    [Fact]
    public async Task CreateAsync_stores_the_term()
    {
        using var db = TestDb.Create();

        var id = await CreateService(db).CreateAsync(new TermDto(0, 1, "Kỳ 1", "Học kỳ đầu", true));

        var term = await db.Terms.SingleAsync(t => t.TermId == id);
        term.TermNo.Should().Be(1);
        term.Description.Should().Be("Học kỳ đầu");
    }

    /// <summary>Zero is a legal kỳ: OTP101 really is Kỳ 0.</summary>
    [Fact]
    public async Task CreateAsync_accepts_term_zero()
    {
        using var db = TestDb.Create();

        var id = await CreateService(db).CreateAsync(new TermDto(0, 0, "Kỳ 0", null, true));

        (await db.Terms.SingleAsync(t => t.TermId == id)).TermNo.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_term_number()
    {
        using var db = TestDb.CreateWithReferenceData();

        var act = () => CreateService(db).CreateAsync(new TermDto(0, 1, "Kỳ 1 lặp", null, true));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã tồn tại*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    public async Task CreateAsync_rejects_a_term_number_outside_the_allowed_range(int termNo)
    {
        using var db = TestDb.Create();

        var act = () => CreateService(db).CreateAsync(new TermDto(0, termNo, "Kỳ lạ", null, true));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>A blank name gets the conventional one rather than being rejected.</summary>
    [Fact]
    public async Task CreateAsync_falls_back_to_the_conventional_name()
    {
        using var db = TestDb.Create();

        var id = await CreateService(db).CreateAsync(new TermDto(0, 3, "  ", null, true));

        (await db.Terms.SingleAsync(t => t.TermId == id)).TermName.Should().Be("Kỳ 3");
    }

    /// <summary>
    /// Curriculum.TermNo is a foreign key onto Term.TermNo, so renumbering a kỳ
    /// that subjects point at would orphan every one of them.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_refuses_to_renumber_a_term_that_is_in_use()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);

        var term = await db.Terms.SingleAsync(t => t.TermNo == 1);

        // 11 is unused by the reference data, so the in-use check is what fires
        // rather than the uniqueness check - which is the behaviour under test.
        var act = () => CreateService(db).UpdateAsync(new TermDto(term.TermId, 11, "Kỳ 11", null, true));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đang có môn học*");
    }

    [Fact]
    public async Task UpdateAsync_can_rename_a_term_that_is_in_use()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("PRF192").CourseId, termNo: 1);

        var term = await db.Terms.SingleAsync(t => t.TermNo == 1);
        await CreateService(db).UpdateAsync(new TermDto(term.TermId, 1, "Học kỳ một", "Mô tả", true));

        var updated = await db.Terms.SingleAsync(t => t.TermId == term.TermId);
        updated.TermName.Should().Be("Học kỳ một");
    }

    [Fact]
    public async Task DeleteAsync_refuses_while_subjects_belong_to_the_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("PRF192").CourseId, termNo: 2);

        var term = await db.Terms.SingleAsync(t => t.TermNo == 2);

        var act = () => CreateService(db).DeleteAsync(term.TermId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*môn học thuộc kỳ này*");
    }

    [Fact]
    public async Task DeleteAsync_removes_an_unused_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var term = await db.Terms.SingleAsync(t => t.TermNo == 9);

        await CreateService(db).DeleteAsync(term.TermId);

        (await db.Terms.AnyAsync(t => t.TermNo == 9)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_reports_how_many_subjects_sit_in_each_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddCurriculumItem(major.MajorId, db.AddCourse("AAA101").CourseId, 1);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("BBB101").CourseId, 1);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("CCC101").CourseId, 2);

        var terms = await CreateService(db).GetAllAsync();

        terms.Single(t => t.TermNo == 1).SubjectCount.Should().Be(2);
        terms.Single(t => t.TermNo == 2).SubjectCount.Should().Be(1);
        terms.Single(t => t.TermNo == 3).SubjectCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_orders_by_term_number()
    {
        using var db = TestDb.CreateWithReferenceData();

        var terms = await CreateService(db).GetAllAsync();

        terms.Select(t => t.TermNo).Should().BeInAscendingOrder();
        terms.First().TermNo.Should().Be(0);
    }

    [Fact]
    public async Task SetActiveAsync_toggles_the_status()
    {
        using var db = TestDb.CreateWithReferenceData();
        var term = await db.Terms.SingleAsync(t => t.TermNo == 5);

        await CreateService(db).SetActiveAsync(term.TermId, false);

        (await db.Terms.SingleAsync(t => t.TermId == term.TermId)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Term_writes_require_an_admin()
    {
        using var db = TestDb.Create();

        var act = () => CreateService(db, asAdmin: false).CreateAsync(new TermDto(0, 1, "Kỳ 1", null, true));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
