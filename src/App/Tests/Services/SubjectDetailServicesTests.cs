using Data;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Tests.Services;

/// <summary>Subject materials - the syllabus bibliography.</summary>
public class SubjectMaterialServiceTests
{
    private static SubjectMaterialService CreateService(FAT_DBContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    private static SubjectMaterialDto Material(int courseId, string title, string? url = null)
        => new(0, courseId, title, null, url, null, null, null, 0, true);

    [Fact]
    public async Task CreateAsync_stores_the_material()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var id = await CreateService(db).CreateAsync(
            Material(course.CourseId, "C Programming Language", "https://example.com/k-r"));

        var stored = await db.SubjectMaterials.SingleAsync(m => m.SubjectMaterialId == id);
        stored.Title.Should().Be("C Programming Language");
        stored.Url.Should().Be("https://example.com/k-r");
    }

    [Fact]
    public async Task CreateAsync_accepts_a_material_with_no_url()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var id = await CreateService(db).CreateAsync(Material(course.CourseId, "Printed textbook"));

        (await db.SubjectMaterials.SingleAsync(m => m.SubjectMaterialId == id)).Url.Should().BeNull();
    }

    /// <summary>
    /// Only http/https: the student screen hands this string to the shell, and a
    /// "file:" or "javascript:" URL would be launched with it.
    /// </summary>
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("/relative/path")]
    public async Task CreateAsync_rejects_a_url_that_is_not_http_or_https(string url)
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(Material(course.CourseId, "Bad link", url));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*http*");
    }

    [Fact]
    public async Task CreateAsync_rejects_an_empty_title()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(Material(course.CourseId, "   "));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_title_within_the_same_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        await service.CreateAsync(Material(course.CourseId, "Textbook"));

        var act = () => service.CreateAsync(Material(course.CourseId, "Textbook"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task GetByCourseAsync_hides_deactivated_materials_by_default()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);

        var id = await service.CreateAsync(Material(course.CourseId, "Retired"));
        await service.UpdateAsync(new SubjectMaterialDto(
            id, course.CourseId, "Retired", null, null, null, null, null, 0, IsActive: false));

        (await service.GetByCourseAsync(course.CourseId)).Should().BeEmpty();
        (await service.GetByCourseAsync(course.CourseId, includeInactive: true)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ReorderAsync_rewrites_the_display_order()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);

        var a = await service.CreateAsync(Material(course.CourseId, "A"));
        var b = await service.CreateAsync(Material(course.CourseId, "B"));

        await service.ReorderAsync(course.CourseId, [b, a]);

        var ordered = await service.GetByCourseAsync(course.CourseId);
        ordered.Select(m => m.Title).Should().ContainInOrder("B", "A");
    }

    [Fact]
    public async Task Material_writes_require_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, asAdmin: false).CreateAsync(Material(course.CourseId, "Book"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

/// <summary>Assessment schedule - the syllabus timeline.</summary>
public class AssessmentScheduleServiceTests
{
    private static AssessmentScheduleService CreateService(FAT_DBContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    private static AssessmentScheduleDto Schedule(
        int courseId, int sessionNo, string title, DateTime? expectedDate = null)
        => new(0, courseId, sessionNo, null, title, null, expectedDate, null, null, null);

    /// <summary>Left to the service so there is one rule for how a week is computed.</summary>
    [Fact]
    public async Task CreateAsync_derives_the_week_from_the_session_number()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var id = await CreateService(db).CreateAsync(Schedule(course.CourseId, 15, "Progress test"));

        (await db.AssessmentSchedules.SingleAsync(s => s.AssessmentScheduleId == id))
            .WeekNo.Should().Be(8, "two sessions a week puts session 15 in week 8");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_session_number_below_one()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(Schedule(course.CourseId, 0, "Bad"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*buổi*");
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_session_within_the_same_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        await service.CreateAsync(Schedule(course.CourseId, 15, "Progress test"));

        var act = () => service.CreateAsync(Schedule(course.CourseId, 15, "Another"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã có lịch kiểm tra*");
    }

    /// <summary>FLM publishes no dates, so an administrator adds them afterwards.</summary>
    [Fact]
    public async Task CreateAsync_accepts_a_schedule_with_no_expected_date()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var id = await CreateService(db).CreateAsync(Schedule(course.CourseId, 15, "Progress test"));

        (await db.AssessmentSchedules.SingleAsync(s => s.AssessmentScheduleId == id))
            .ExpectedDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_stores_a_date_the_administrator_supplies()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        var id = await service.CreateAsync(Schedule(course.CourseId, 15, "Progress test"));

        var date = new DateTime(2025, 3, 15);
        await service.UpdateAsync(new AssessmentScheduleDto(
            id, course.CourseId, 15, null, "Progress test", null, date, "Offline", null, null));

        (await db.AssessmentSchedules.SingleAsync()).ExpectedDate.Should().Be(date);
    }

    /// <summary>
    /// A schedule row pointing at another subject's grade column would show a
    /// checkpoint that grades nothing.
    /// </summary>
    [Fact]
    public async Task CreateAsync_rejects_a_grade_column_from_another_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var other = db.AddCourse("PRO192");

        db.Assessments.Add(new Domain.Entities.Assessment
        {
            CourseId = other.CourseId,
            Name = "Final exam",
            Weight = 1.00m
        });
        await db.SaveChangesAsync();

        var foreignAssessmentId = (await db.Assessments.SingleAsync()).AssessmentId;

        var act = () => CreateService(db).CreateAsync(new AssessmentScheduleDto(
            0, course.CourseId, 30, null, "Final", null, null, null, foreignAssessmentId, null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*không thuộc môn học này*");
    }

    [Fact]
    public async Task GetByCourseAsync_orders_by_session()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        await service.CreateAsync(Schedule(course.CourseId, 30, "Final exam"));
        await service.CreateAsync(Schedule(course.CourseId, 15, "Progress test"));

        var schedule = await service.GetByCourseAsync(course.CourseId);

        schedule.Select(s => s.SessionNo).Should().ContainInOrder(15, 30);
    }

    [Fact]
    public async Task Schedule_writes_require_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, asAdmin: false).CreateAsync(Schedule(course.CourseId, 1, "Test"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
