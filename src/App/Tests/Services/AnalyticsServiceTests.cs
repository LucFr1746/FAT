using Data;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_counts_statuses_and_identifies_score_extremes()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        major.RequiredCredits = 12;
        db.SaveChanges();
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(
            student.StudentId,
            db.AddCourse("HIGH", 3).CourseId,
            semester.SemesterId,
            EnrollmentStatus.Passed,
            9m);
        db.AddEnrollment(
            student.StudentId,
            db.AddCourse("LOW", 3).CourseId,
            semester.SemesterId,
            EnrollmentStatus.Failed,
            3m);
        db.AddEnrollment(
            student.StudentId,
            db.AddCourse("DOING", 3).CourseId,
            semester.SemesterId,
            EnrollmentStatus.Studying);

        var service = CreateService(db, student.StudentId);
        var dashboard = await service.GetDashboardAsync(student.StudentId);
        var top = await service.GetTopCoursesAsync(student.StudentId, 1);
        var weakest = await service.GetWeakestCoursesAsync(student.StudentId, 1);

        dashboard.TotalCourses.Should().Be(3);
        dashboard.PassedCourses.Should().Be(1);
        dashboard.FailedCourses.Should().Be(1);
        dashboard.StudyingCourses.Should().Be(1);
        dashboard.AverageFinalScore.Should().Be(6m);
        dashboard.EarnedCredits.Should().Be(3);
        dashboard.RemainingCredits.Should().Be(9);
        top.Single().CourseCode.Should().Be("HIGH");
        weakest.Single().CourseCode.Should().Be("LOW");
    }

    [Fact]
    public async Task GetDashboardAsync_handles_an_empty_and_null_grade_history()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);

        var dashboard = await CreateService(db, student.StudentId)
            .GetDashboardAsync(student.StudentId);

        dashboard.CumulativeGpa.Should().BeNull();
        dashboard.AverageFinalScore.Should().BeNull();
        dashboard.TotalCourses.Should().Be(0);
        dashboard.PassedCourses.Should().Be(0);
        dashboard.FailedCourses.Should().Be(0);
        dashboard.GpaTrend.Should().BeEmpty();
        dashboard.GradeDistribution.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_ignores_unverified_aggregate_scores_for_registered_student()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        student.CurrentTermNo = 1;
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192", 3);
        db.Assessments.Add(new Assessment
        {
            CourseId = course.CourseId,
            Name = "Final exam",
            Weight = 1m,
            DisplayOrder = 1
        });
        db.AddEnrollment(
            student.StudentId,
            course.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Passed,
            finalScore: 8m);
        await db.SaveChangesAsync();

        var dashboard = await CreateService(db, student.StudentId)
            .GetDashboardAsync(student.StudentId);

        dashboard.CumulativeGpa.Should().BeNull();
        dashboard.AverageFinalScore.Should().BeNull();
        dashboard.PassedCourses.Should().Be(0);
        dashboard.TotalCourses.Should().Be(0);
        dashboard.EarnedCredits.Should().Be(0);
    }

    [Fact]
    public async Task GetGradeDistributionAsync_uses_only_current_counted_attempts()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192", 3);

        var oldAttempt = db.AddEnrollment(
            student.StudentId,
            course.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Failed,
            3m,
            isCounted: false);
        oldAttempt.LetterGrade = "F";

        var currentCourse = db.AddCourse("PRO192", 3);
        var currentAttempt = db.AddEnrollment(
            student.StudentId,
            currentCourse.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Passed,
            8m);
        currentAttempt.LetterGrade = "B+";
        db.SaveChanges();

        var distribution = await CreateService(db, student.StudentId)
            .GetGradeDistributionAsync(student.StudentId);

        distribution.Should().ContainSingle();
        distribution[0].LetterGrade.Should().Be("B+");
        distribution[0].Percent.Should().Be(100m);
    }

    private static AnalyticsService CreateService(FAT_DBContext db, int studentId)
        => new(
            db,
            TestCurrentUserContext.Student(studentId),
            new GpaService(db));
}
