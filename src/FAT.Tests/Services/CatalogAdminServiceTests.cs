using FAT.Data;
using FAT.Domain.Enums;
using FAT.Services.Dtos;
using FAT.Services.Implementations;
using FAT.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Services;

/// <summary>
/// Manage Major, Manage Semester (calendar) and Manage Subject.
///
/// The in-memory provider does not enforce unique indexes, so a test that passes
/// here proves the SERVICE rejected the duplicate - which is the point, since a
/// constraint violation reaching the user is not an error message they can act
/// on.
/// </summary>
public class CatalogAdminServiceTests
{
    private static CatalogAdminService CreateService(FatDbContext db, bool asAdmin = true)
    {
        var user = asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1);
        return new CatalogAdminService(db, user, new CurriculumAdminService(db, user));
    }

    // =========================================================================
    // Major
    // =========================================================================

    [Fact]
    public async Task CreateMajorAsync_stores_the_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db);

        var id = await service.CreateMajorAsync(new MajorDto(0, "SE", "Software Engineering", 1, 9, true));

        var major = await db.Majors.SingleAsync(m => m.MajorId == id);
        major.MajorCode.Should().Be("SE");
        major.MajorName.Should().Be("Software Engineering");
    }

    [Fact]
    public async Task CreateMajorAsync_rejects_a_duplicate_code()
    {
        using var db = TestDb.CreateWithReferenceData();
        db.AddMajor("SE");
        var service = CreateService(db);

        var act = () => service.CreateMajorAsync(new MajorDto(0, "SE", "Another", 1, 9, true));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã tồn tại*");
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    [InlineData("SE", "")]
    public async Task CreateMajorAsync_rejects_missing_required_fields(string code, string name)
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db);

        var act = () => service.CreateMajorAsync(new MajorDto(0, code, name, 1, 9, true));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// RequiredCredits is derived from the curriculum. Accepting it from the form
    /// is how it drifts away from the subject list, and a drifted value makes the
    /// graduation percentage wrong for everyone in the programme.
    /// </summary>
    [Fact]
    public async Task UpdateMajorAsync_ignores_the_credits_supplied_by_the_caller()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192", credits: 3);
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);

        var service = CreateService(db);

        await service.UpdateMajorAsync(new MajorDto(major.MajorId, "SE", "Renamed", 999, 9, true));

        var updated = await db.Majors.SingleAsync();
        updated.RequiredCredits.Should().Be(3, "credits come from the curriculum, not the form");
    }

    [Fact]
    public async Task DeactivateMajorAsync_refuses_while_students_are_enrolled()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        db.AddStudent(major.MajorId);

        var act = () => CreateService(db).DeactivateMajorAsync(major.MajorId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sinh viên đang theo học*");
    }

    [Fact]
    public async Task DeactivateMajorAsync_soft_deletes_when_nobody_is_enrolled()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");

        await CreateService(db).DeactivateMajorAsync(major.MajorId);

        (await db.Majors.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Major_writes_require_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var service = CreateService(db, asAdmin: false);

        var act = () => service.CreateMajorAsync(new MajorDto(0, "SE", "Software", 1, 9, true));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // =========================================================================
    // Semester (calendar)
    // =========================================================================

    /// <summary>
    /// The database assumes exactly one current semester, and
    /// db/02_seed_master.sql asserts it. Two saves instead of one would leave a
    /// window with two.
    /// </summary>
    [Fact]
    public async Task SetCurrentSemesterAsync_leaves_exactly_one_semester_current()
    {
        using var db = TestDb.CreateWithReferenceData();
        db.AddSemester("SP25", 1, isCurrent: true);
        var target = db.AddSemester("SU25", 2);
        db.AddSemester("FA25", 3);

        await CreateService(db).SetCurrentSemesterAsync(target.SemesterId);

        var current = await db.Semesters.Where(s => s.IsCurrent).ToListAsync();
        current.Should().ContainSingle().Which.SemesterCode.Should().Be("SU25");
    }

    [Fact]
    public async Task CreateSemesterAsync_rejects_an_end_date_before_the_start()
    {
        using var db = TestDb.CreateWithReferenceData();

        var act = () => CreateService(db).CreateSemesterAsync(new SemesterDto(
            0, "SP25", "Spring 2025", new DateTime(2025, 5, 1), new DateTime(2025, 1, 1), 1, false));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*sau ngày bắt đầu*");
    }

    /// <summary>
    /// DisplayOrder is the real chronological order and is uniquely indexed; a
    /// duplicate makes the GPA trend chart's order undefined.
    /// </summary>
    [Fact]
    public async Task CreateSemesterAsync_rejects_a_duplicate_display_order()
    {
        using var db = TestDb.CreateWithReferenceData();
        db.AddSemester("SP25", 1);

        var act = () => CreateService(db).CreateSemesterAsync(new SemesterDto(
            0, "SU25", "Summer 2025", new DateTime(2025, 5, 1), new DateTime(2025, 8, 1), 1, false));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Thứ tự học kỳ*");
    }

    // =========================================================================
    // Subject
    // =========================================================================

    [Fact]
    public async Task CreateCourseAsync_normalises_the_code_to_upper_case()
    {
        using var db = TestDb.CreateWithReferenceData();

        var id = await CreateService(db).CreateCourseAsync(new CourseDto(
            0, "prf192", "Programming Fundamentals", 3, null, true, 0));

        (await db.Courses.SingleAsync(c => c.CourseId == id)).CourseCode.Should().Be("PRF192");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task CreateCourseAsync_rejects_credits_outside_the_allowed_range(int credits)
    {
        using var db = TestDb.CreateWithReferenceData();

        var act = () => CreateService(db).CreateCourseAsync(new CourseDto(
            0, "PRF192", "Programming", credits, null, true, 0));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*tín chỉ*");
    }

    /// <summary>Zero credits is legal: the Kỳ 0 orientation block carries none.</summary>
    [Fact]
    public async Task CreateCourseAsync_accepts_a_zero_credit_subject()
    {
        using var db = TestDb.CreateWithReferenceData();

        var id = await CreateService(db).CreateCourseAsync(new CourseDto(
            0, "OTP101", "Orientation", 0, null, true, 0, CountsTowardGpa: false));

        var course = await db.Courses.SingleAsync(c => c.CourseId == id);
        course.Credits.Should().Be(0);
        course.CountsTowardGpa.Should().BeFalse();
    }

    /// <summary>
    /// A subject's credits feed every programme that teaches it, so changing them
    /// has to resync each of those totals.
    /// </summary>
    [Fact]
    public async Task UpdateCourseAsync_resyncs_credits_for_every_affected_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var ai = db.AddMajor("AI");
        var course = db.AddCourse("MAD101", credits: 3);
        db.AddCurriculumItem(se.MajorId, course.CourseId, 1);
        db.AddCurriculumItem(ai.MajorId, course.CourseId, 2);

        await CreateService(db).SyncMajorRequiredCreditsAsync(se.MajorId);
        await CreateService(db).SyncMajorRequiredCreditsAsync(ai.MajorId);

        await CreateService(db).UpdateCourseAsync(new CourseDto(
            course.CourseId, "MAD101", "Discrete Mathematics", 5, null, true, 0));

        (await db.Majors.SingleAsync(m => m.MajorId == se.MajorId)).RequiredCredits.Should().Be(5);
        (await db.Majors.SingleAsync(m => m.MajorId == ai.MajorId)).RequiredCredits.Should().Be(5);
    }

    [Fact]
    public async Task DeactivateCourseAsync_refuses_while_students_are_studying_it()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var student = db.AddStudent(major.MajorId);
        db.AddEnrollment(student.StudentId, course.CourseId, semester.SemesterId, EnrollmentStatus.Studying);

        var act = () => CreateService(db).DeactivateCourseAsync(course.CourseId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đang học*");
    }

    /// <summary>Never a hard delete: it would take every transcript with it.</summary>
    [Fact]
    public async Task DeactivateCourseAsync_soft_deletes_the_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        await CreateService(db).DeactivateCourseAsync(course.CourseId);

        var stored = await db.Courses.SingleAsync();
        stored.IsActive.Should().BeFalse();
        (await db.Courses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetCoursesAsync_filters_by_major_through_the_curriculum()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var inCurriculum = db.AddCourse("PRF192");
        db.AddCourse("UNRELATED");
        db.AddCurriculumItem(se.MajorId, inCurriculum.CourseId, 1);

        var results = await CreateService(db).GetCoursesAsync(new CourseFilter(MajorId: se.MajorId));

        results.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }

    /// <summary>
    /// Kỳ 0 is a real term, so a filter for it must return the orientation
    /// subject rather than being treated as "no filter".
    /// </summary>
    [Fact]
    public async Task GetCoursesAsync_can_filter_on_term_zero()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var orientation = db.AddCourse("OTP101", credits: 0);
        var normal = db.AddCourse("PRF192");
        db.AddCurriculumItem(major.MajorId, orientation.CourseId, termNo: 0);
        db.AddCurriculumItem(major.MajorId, normal.CourseId, termNo: 1);

        var results = await CreateService(db).GetCoursesAsync(
            new CourseFilter(MajorId: major.MajorId, TermNo: 0));

        results.Should().ContainSingle().Which.CourseCode.Should().Be("OTP101");
    }

    // =========================================================================
    // Prerequisites
    // =========================================================================

    [Fact]
    public async Task AddPrerequisiteAsync_stores_a_plain_requirement()
    {
        using var db = TestDb.CreateWithReferenceData();
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");

        await CreateService(db).AddPrerequisiteAsync(pro.CourseId, prf.CourseId);

        var edge = await db.Prerequisites.SingleAsync();
        edge.GroupNo.Should().Be(0, "a lone requirement is not a choice group");
    }

    [Fact]
    public async Task AddPrerequisiteAsync_rejects_a_self_reference()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).AddPrerequisiteAsync(course.CourseId, course.CourseId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*chính nó*");
    }

    /// <summary>The loop the database cannot see.</summary>
    [Fact]
    public async Task AddPrerequisiteAsync_rejects_an_edge_that_would_close_a_cycle()
    {
        using var db = TestDb.CreateWithReferenceData();
        var a = db.AddCourse("AAA101");
        var b = db.AddCourse("BBB101");
        db.AddPrerequisite(b.CourseId, a.CourseId);

        var act = () => CreateService(db).AddPrerequisiteAsync(a.CourseId, b.CourseId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*vòng lặp*");
    }

    [Fact]
    public async Task AddPrerequisiteAsync_rejects_a_duplicate_pair()
    {
        using var db = TestDb.CreateWithReferenceData();
        var a = db.AddCourse("AAA101");
        var b = db.AddCourse("BBB101");
        db.AddPrerequisite(b.CourseId, a.CourseId);

        var act = () => CreateService(db).AddPrerequisiteAsync(b.CourseId, a.CourseId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã tồn tại*");
    }

    /// <summary>"MKT101 or MKG101 or MMK101" - one group, three alternatives.</summary>
    [Fact]
    public async Task AddPrerequisiteGroupAsync_stores_alternatives_under_one_group_number()
    {
        using var db = TestDb.CreateWithReferenceData();
        var target = db.AddCourse("MKT205");
        var a = db.AddCourse("MKT101");
        var b = db.AddCourse("MKG101");
        var c = db.AddCourse("MMK101");

        var groupNo = await CreateService(db).AddPrerequisiteGroupAsync(
            target.CourseId, [a.CourseId, b.CourseId, c.CourseId]);

        var edges = await db.Prerequisites.Where(p => p.CourseId == target.CourseId).ToListAsync();
        edges.Should().HaveCount(3);
        edges.Should().OnlyContain(e => e.GroupNo == groupNo);
        groupNo.Should().BeGreaterThan(0);
    }

    /// <summary>A "group" of one is a plain requirement, not a one-way choice.</summary>
    [Fact]
    public async Task AddPrerequisiteGroupAsync_stores_a_single_member_as_a_plain_requirement()
    {
        using var db = TestDb.CreateWithReferenceData();
        var target = db.AddCourse("PRO192");
        var required = db.AddCourse("PRF192");

        await CreateService(db).AddPrerequisiteGroupAsync(target.CourseId, [required.CourseId]);

        (await db.Prerequisites.SingleAsync()).GroupNo.Should().Be(0);
    }
}
