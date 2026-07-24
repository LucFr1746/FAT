using Data;
using Domain.Entities;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Tests.Services;

/// <summary>
/// Grade structure.
///
/// The invariant under test is that weights total 100%. Nothing at run time
/// notices when they do not: the final score is SUM(Score * Weight), so a
/// structure adding to 90% silently caps that subject at 9.0 for everyone.
/// </summary>
public class GradeStructureServiceTests
{
    private static GradeStructureService CreateService(FAT_DBContext db, bool asAdmin = true)
        => new(db, asAdmin ? TestCurrentUserContext.Admin() : TestCurrentUserContext.Student(1));

    private static AssessmentDto Column(int courseId, string name, decimal weight, decimal? minScore = null)
        => new(0, courseId, name, weight, minScore, 0);

    [Fact]
    public async Task CreateAsync_stores_the_grade_column()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var id = await CreateService(db).CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        var stored = await db.Assessments.SingleAsync(a => a.AssessmentId == id);
        stored.Weight.Should().Be(0.40m);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_name_within_the_same_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        await service.CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        var act = () => service.CreateAsync(Column(course.CourseId, "Assignment", 0.30m));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã tồn tại*");
    }

    /// <summary>The same column name in a DIFFERENT subject is perfectly normal.</summary>
    [Fact]
    public async Task CreateAsync_allows_the_same_name_in_another_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var a = db.AddCourse("PRF192");
        var b = db.AddCourse("PRO192");
        var service = CreateService(db);

        await service.CreateAsync(Column(a.CourseId, "Final exam", 1.00m));
        var act = () => service.CreateAsync(Column(b.CourseId, "Final exam", 1.00m));

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task CreateAsync_rejects_a_weight_outside_the_allowed_range(decimal weight)
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(Column(course.CourseId, "Broken", weight));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Trọng số*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task CreateAsync_rejects_a_minimum_score_outside_zero_to_ten(decimal minScore)
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(
            Column(course.CourseId, "Final exam", 0.50m, minScore));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// A structure is built one column at a time, so the FIRST column must be
    /// accepted even though it does not yet total 100%.
    /// </summary>
    [Fact]
    public async Task CreateAsync_allows_an_unbalanced_structure_while_it_is_being_built()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(
            Column(course.CourseId, "Assignment", 0.40m), allowUnbalanced: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unbalanced_structure_when_the_caller_says_it_is_final()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db).CreateAsync(
            Column(course.CourseId, "Assignment", 0.40m), allowUnbalanced: false);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*100%*");
    }

    [Fact]
    public async Task ValidateWeightsAsync_reports_a_balanced_structure()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var service = CreateService(db);
        await service.CreateAsync(Column(course.CourseId, "Assignment", 0.40m));
        await service.CreateAsync(Column(course.CourseId, "Final exam", 0.60m));

        var validation = await service.ValidateWeightsAsync(course.CourseId);

        validation.IsBalanced.Should().BeTrue();
        validation.TotalWeightPercent.Should().Be(100m);
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateWeightsAsync_reports_an_unbalanced_structure()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        await CreateService(db).CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        var validation = await CreateService(db).ValidateWeightsAsync(course.CourseId);

        validation.IsBalanced.Should().BeFalse();
        validation.TotalWeightPercent.Should().Be(40m);
        validation.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// A 33.3/33.3/33.4 split is a real FLM structure and must be accepted; only
    /// the tolerance makes that possible.
    /// </summary>
    [Fact]
    public async Task ValidateWeightsAsync_accepts_a_three_way_split()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("DTR103");
        var service = CreateService(db);
        await service.CreateAsync(Column(course.CourseId, "Part 1", 0.3333m));
        await service.CreateAsync(Column(course.CourseId, "Part 2", 0.3333m));
        await service.CreateAsync(Column(course.CourseId, "Part 3", 0.3334m));

        (await service.ValidateWeightsAsync(course.CourseId)).IsBalanced.Should().BeTrue();
    }

    /// <summary>
    /// Deleting a column that already carries scores would change the final score
    /// of every student who has one, with nobody told.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_refuses_when_scores_have_already_been_recorded()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var course = db.AddCourse("PRF192");
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var student = db.AddStudent(major.MajorId);

        var assessmentId = await CreateService(db).CreateAsync(
            Column(course.CourseId, "Final exam", 1.00m));

        var enrollment = db.AddEnrollment(
            student.StudentId, course.CourseId, semester.SemesterId, Domain.Enums.EnrollmentStatus.Studying);

        db.Grades.Add(new Grade
        {
            EnrollmentId = enrollment.EnrollmentId,
            AssessmentId = assessmentId,
            Score = 8.0m,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var act = () => CreateService(db).DeleteAsync(assessmentId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*điểm thành phần*");
    }

    [Fact]
    public async Task DeleteAsync_removes_a_column_that_has_no_scores()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var id = await CreateService(db).CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        await CreateService(db).DeleteAsync(id);

        (await db.Assessments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_weight()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");
        var id = await CreateService(db).CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        await CreateService(db).UpdateAsync(new AssessmentDto(id, course.CourseId, "Assignment", 0.50m, null, 0));

        (await db.Assessments.SingleAsync()).Weight.Should().Be(0.50m);
    }

    [Fact]
    public async Task Grade_structure_writes_require_an_admin()
    {
        using var db = TestDb.CreateWithReferenceData();
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, asAdmin: false)
            .CreateAsync(Column(course.CourseId, "Assignment", 0.40m));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
