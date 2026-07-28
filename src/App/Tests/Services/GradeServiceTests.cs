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

    [Fact]
    public async Task GetStudentGradesAsync_hides_aggregate_result_without_component_grades()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        student.CurrentTermNo = 1;
        var course = db.AddCourse("PRF192");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);
        var assessment = AddAssessment(db, course.CourseId, "Final exam", 1m);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var enrollment = db.AddEnrollment(
            student.StudentId,
            course.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Passed,
            finalScore: 8m);
        await db.SaveChangesAsync();
        var service = CreateService(db, student.StudentId);

        var before = await service.GetStudentGradesAsync(student.StudentId);

        before.Should().ContainSingle();
        before[0].StatusDisplay.Should().Be("Not Graded");
        before[0].FinalScore.Should().BeNull();

        db.Grades.Add(new Grade
        {
            EnrollmentId = enrollment.EnrollmentId,
            AssessmentId = assessment.AssessmentId,
            Score = 8m,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var after = await service.GetStudentGradesAsync(student.StudentId);
        after[0].StatusDisplay.Should().Be("Passed");
        after[0].FinalScore.Should().Be(8m);
    }

    [Fact]
    public async Task GetStudentGradesAsync_includes_the_complete_nine_term_curriculum()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);

        for (var termNo = 1; termNo <= 9; termNo++)
        {
            var course = db.AddCourse($"TERM{termNo}");
            db.AddCurriculumItem(major.MajorId, course.CourseId, termNo);
            AddAssessment(db, course.CourseId, "Final exam", 1m);
        }

        var rows = await CreateService(db, student.StudentId)
            .GetStudentGradesAsync(student.StudentId);

        rows.Should().HaveCount(9);
        rows.Select(r => r.CurriculumTermNo).Should().BeEquivalentTo(
            Enumerable.Range(1, 9));
        rows.Should().OnlyContain(r => !r.IsEnrolled
                                      && r.StatusDisplay == "Not Graded");
        (await db.Enrollments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpsertStudentGradeAsync_creates_an_enrollment_for_a_curriculum_placeholder()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("NEW101");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var assessment = AddAssessment(db, course.CourseId, "Final exam", 1m);
        var service = CreateService(db, student.StudentId);

        var enrollmentId = await service.UpsertStudentGradeAsync(
            student.StudentId,
            enrollmentId: 0,
            course.CourseId,
            semester.SemesterId,
            assessment.AssessmentId,
            score: 8m);

        enrollmentId.Should().BeGreaterThan(0);
        (await db.Enrollments.SingleAsync()).StudentId.Should().Be(student.StudentId);
        (await db.Grades.SingleAsync()).Score.Should().Be(8m);

        var rows = await service.GetStudentGradesAsync(student.StudentId);
        rows.Should().ContainSingle();
        rows[0].IsEnrolled.Should().BeTrue();
        rows[0].FinalScore.Should().Be(8m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public async Task UpsertStudentGradeAsync_does_not_create_an_enrollment_for_an_invalid_score(
        decimal score)
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("NEW101");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var assessment = AddAssessment(db, course.CourseId, "Final exam", 1m);

        var act = () => CreateService(db, student.StudentId)
            .UpsertStudentGradeAsync(
                student.StudentId,
                enrollmentId: 0,
                course.CourseId,
                semester.SemesterId,
                assessment.AssessmentId,
                score);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await db.Enrollments.CountAsync()).Should().Be(0);
        (await db.Grades.CountAsync()).Should().Be(0);
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
