using Data;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

public class GradeServiceTests
{
    [Fact]
    public async Task UpsertGradeAsync_calculates_the_weighted_final_score()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assignment = AddAssessment(db, setup.CourseId, "Assignment", 0.40m);
        var finalExam = AddAssessment(db, setup.CourseId, "Final exam", 0.60m);
        var service = CreateService(db, setup.StudentId);

        await service.UpsertGradeAsync(setup.EnrollmentId, assignment.AssessmentId, 8m);
        await service.UpsertGradeAsync(setup.EnrollmentId, finalExam.AssessmentId, 6m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().Be(6.8m);
        enrollment.Status.Should().Be(EnrollmentStatus.Passed);
        enrollment.LetterGrade.Should().Be("C");
        enrollment.GradePoint.Should().Be(2m);
    }

    [Fact]
    public async Task UpsertGradeAsync_keeps_an_incomplete_course_unsettled()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assignment = AddAssessment(db, setup.CourseId, "Assignment", 0.40m);
        AddAssessment(db, setup.CourseId, "Final exam", 0.60m);

        await CreateService(db, setup.StudentId)
            .UpsertGradeAsync(setup.EnrollmentId, assignment.AssessmentId, 10m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().BeNull();
        enrollment.GradePoint.Should().BeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Studying);
    }

    [Fact]
    public async Task UpsertGradeAsync_fails_when_an_assessment_minimum_is_violated()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var coursework = AddAssessment(db, setup.CourseId, "Coursework", 0.70m);
        var finalExam = AddAssessment(db, setup.CourseId, "Final exam", 0.30m, minScore: 4m);
        var service = CreateService(db, setup.StudentId);

        await service.UpsertGradeAsync(setup.EnrollmentId, coursework.AssessmentId, 10m);
        await service.UpsertGradeAsync(setup.EnrollmentId, finalExam.AssessmentId, 3m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().Be(7.9m);
        enrollment.Status.Should().Be(EnrollmentStatus.Failed);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public async Task UpsertGradeAsync_rejects_scores_outside_the_schema_range(decimal score)
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assessment = AddAssessment(db, setup.CourseId, "Final exam", 1m);

        var act = () => CreateService(db, setup.StudentId)
            .UpsertGradeAsync(setup.EnrollmentId, assessment.AssessmentId, score);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await db.Grades.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task UpsertGradeAsync_accepts_boundary_scores(decimal score)
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assessment = AddAssessment(db, setup.CourseId, "Final exam", 1m);

        await CreateService(db, setup.StudentId)
            .UpsertGradeAsync(setup.EnrollmentId, assessment.AssessmentId, score);

        (await db.Grades.SingleAsync()).Score.Should().Be(score);
        (await db.Enrollments.SingleAsync()).FinalScore.Should().Be(score);
    }

    [Fact]
    public async Task DeleteGradeAsync_returns_the_course_to_studying()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assessment = AddAssessment(db, setup.CourseId, "Final exam", 1m);
        var service = CreateService(db, setup.StudentId);
        await service.UpsertGradeAsync(setup.EnrollmentId, assessment.AssessmentId, 8m);

        await service.DeleteGradeAsync(setup.EnrollmentId, assessment.AssessmentId);

        (await db.Grades.CountAsync()).Should().Be(0);
        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().BeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Studying);
    }

    [Fact]
    public async Task GetStudentGradesAsync_is_null_safe_when_a_course_has_no_assessments()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);

        var rows = await CreateService(db, setup.StudentId)
            .GetStudentGradesAsync(setup.StudentId);

        rows.Should().ContainSingle();
        rows[0].Assessments.Should().BeEmpty();
        rows[0].StatusDisplay.Should().Be("Not Graded");
        rows[0].FinalScoreDisplay.Should().Be("-");
    }

    private static GradeService CreateService(FAT_DBContext db, int studentId)
        => new(
            db,
            TestCurrentUserContext.Student(studentId),
            new PrerequisiteService(db),
            new GpaService(db));

    private static (int StudentId, int CourseId, int EnrollmentId) CreateEnrollment(FAT_DBContext db)
    {
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("PRF192");
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var enrollment = db.AddEnrollment(
            student.StudentId,
            course.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Studying);

        return (student.StudentId, course.CourseId, enrollment.EnrollmentId);
    }

    private static Assessment AddAssessment(
        FAT_DBContext db,
        int courseId,
        string name,
        decimal weight,
        decimal? minScore = null)
    {
        var assessment = new Assessment
        {
            CourseId = courseId,
            Name = name,
            Weight = weight,
            MinScoreToPass = minScore,
            DisplayOrder = db.Assessments.Count()
        };
        db.Assessments.Add(assessment);
        db.SaveChanges();
        return assessment;
    }
}
