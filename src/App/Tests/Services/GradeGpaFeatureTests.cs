using Data;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

public class GradeGpaFeatureTests
{
    [Fact]
    public void CalculateFinalScore_returns_null_for_an_empty_component_list()
    {
        GradeCalculation.CalculateFinalScore([]).Should().BeNull();
    }

    [Fact]
    public void CalculateFinalScore_returns_null_when_any_score_is_missing()
    {
        var components = new (decimal Weight, decimal? Score)[]
        {
            (0.40m, 8m),
            (0.60m, null)
        };

        GradeCalculation.CalculateFinalScore(components).Should().BeNull();
    }

    [Fact]
    public void CalculateFinalScore_rejects_a_null_component_sequence()
    {
        var action = () => GradeCalculation.CalculateFinalScore(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalculateFinalScore_uses_assessment_weights_and_consistent_rounding()
    {
        var components = new (decimal Weight, decimal? Score)[]
        {
            (0.25m, 8.25m),
            (0.75m, 6.75m)
        };

        GradeCalculation.CalculateFinalScore(components).Should().Be(7.1m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public void ValidateScore_rejects_values_outside_the_ten_point_scale(double score)
    {
        var action = () => GradeCalculation.ValidateScore((decimal)score);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ValidateScore_accepts_boundary_values(double score)
    {
        var action = () => GradeCalculation.ValidateScore((decimal)score);

        action.Should().NotThrow();
    }

    [Fact]
    public async Task UpsertGradeAsync_calculates_weighted_final_score()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        AddAssessment(db, setup.Course.CourseId, "Assignment", 0.40m, displayOrder: 1);
        AddAssessment(db, setup.Course.CourseId, "Final exam", 0.60m, displayOrder: 2);
        var assessments = await db.Assessments.OrderBy(item => item.DisplayOrder).ToListAsync();
        var service = CreateGradeService(db);

        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, assessments[0].AssessmentId, 8m);
        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, assessments[1].AssessmentId, 6m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().Be(6.8m);
        enrollment.Status.Should().Be(EnrollmentStatus.Passed);
        enrollment.LetterGrade.Should().Be("C");
        enrollment.GradePoint.Should().Be(2m);
    }

    [Fact]
    public async Task UpsertGradeAsync_keeps_an_incomplete_course_studying()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        var first = AddAssessment(db, setup.Course.CourseId, "Assignment", 0.50m, displayOrder: 1);
        AddAssessment(db, setup.Course.CourseId, "Final exam", 0.50m, displayOrder: 2);

        await CreateGradeService(db).UpsertGradeAsync(
            setup.Enrollment.EnrollmentId,
            first.AssessmentId,
            8m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().BeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Studying);
    }

    [Fact]
    public async Task UpsertGradeAsync_updates_instead_of_duplicating_a_grade()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        var assessment = AddAssessment(db, setup.Course.CourseId, "Final exam", 1m);
        var service = CreateGradeService(db);

        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, assessment.AssessmentId, 6m);
        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, assessment.AssessmentId, 9m);

        (await db.Grades.CountAsync()).Should().Be(1);
        (await db.Grades.SingleAsync()).Score.Should().Be(9m);
        (await db.Enrollments.SingleAsync()).FinalScore.Should().Be(9m);
    }

    [Fact]
    public async Task UpsertGradeAsync_honours_the_component_minimum()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        var assignment = AddAssessment(db, setup.Course.CourseId, "Assignment", 0.50m);
        var final = AddAssessment(
            db,
            setup.Course.CourseId,
            "Final exam",
            0.50m,
            minScore: 4m,
            displayOrder: 2);
        var service = CreateGradeService(db);

        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, assignment.AssessmentId, 10m);
        await service.UpsertGradeAsync(setup.Enrollment.EnrollmentId, final.AssessmentId, 3m);

        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().Be(6.5m);
        enrollment.Status.Should().Be(EnrollmentStatus.Failed);
    }

    [Fact]
    public async Task UpsertGradeAsync_honours_a_course_specific_pass_score()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        setup.Course.MinAvgMarkToPass = 6m;
        db.SaveChanges();
        var assessment = AddAssessment(db, setup.Course.CourseId, "Final exam", 1m);

        await CreateGradeService(db).UpsertGradeAsync(
            setup.Enrollment.EnrollmentId,
            assessment.AssessmentId,
            5.5m);

        (await db.Enrollments.SingleAsync()).Status.Should().Be(EnrollmentStatus.Failed);
    }

    [Fact]
    public async Task DeleteGradeAsync_removes_score_and_clears_final_result()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        var assessment = AddAssessment(db, setup.Course.CourseId, "Final exam", 1m);
        var gradeService = CreateGradeService(db);
        var workspace = new GradeWorkspaceService(
            db,
            gradeService,
            TestCurrentUserContext.Admin());

        await gradeService.UpsertGradeAsync(
            setup.Enrollment.EnrollmentId,
            assessment.AssessmentId,
            8m);
        await workspace.DeleteGradeAsync(
            setup.Enrollment.EnrollmentId,
            assessment.AssessmentId);

        (await db.Grades.CountAsync()).Should().Be(0);
        var enrollment = await db.Enrollments.SingleAsync();
        enrollment.FinalScore.Should().BeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Studying);
    }

    [Fact]
    public async Task GetStudentGradesAsync_includes_ungraded_assessments_and_not_graded_status()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = AddCourseAttempt(db);
        AddAssessment(db, setup.Course.CourseId, "Final exam", 1m);
        var workspace = new GradeWorkspaceService(
            db,
            CreateGradeService(db),
            TestCurrentUserContext.Student(setup.Student.StudentId));

        var result = await workspace.GetStudentGradesAsync(setup.Student.StudentId);

        result.Should().ContainSingle();
        result[0].Assessments.Should().ContainSingle();
        result[0].Assessments[0].Score.Should().BeNull();
        result[0].StatusLabel.Should().Be("Not Graded");
    }

    [Fact]
    public async Task GetTranscriptAsync_groups_semesters_chronologically_and_exposes_grade_points()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var later = db.AddSemester("SP26", 2, isCurrent: true);
        var earlier = db.AddSemester("FA25", 1);
        var firstCourse = db.AddCourse("AAA101", credits: 3);
        var secondCourse = db.AddCourse("BBB101", credits: 3);
        var first = db.AddEnrollment(
            student.StudentId,
            firstCourse.CourseId,
            earlier.SemesterId,
            EnrollmentStatus.Passed,
            8m);
        var second = db.AddEnrollment(
            student.StudentId,
            secondCourse.CourseId,
            later.SemesterId,
            EnrollmentStatus.Passed,
            9m);
        first.LetterGrade = "B";
        first.GradePoint = 3m;
        second.LetterGrade = "A";
        second.GradePoint = 4m;
        db.SaveChanges();

        var transcript = await CreateGradeService(db).GetTranscriptAsync(student.StudentId);

        transcript.Semesters.Select(item => item.SemesterCode)
            .Should().ContainInOrder("FA25", "SP26");
        transcript.Semesters[0].Items[0].GradePoint.Should().Be(3m);
        transcript.Semesters[1].SemesterGpa.Should().Be(9m);
    }

    [Fact]
    public async Task GetStatisticsAsync_handles_an_empty_student()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);

        var statistics = await CreateAnalyticsService(db).GetStatisticsAsync(student.StudentId);

        statistics.TotalCourses.Should().Be(0);
        statistics.CumulativeGpa.Should().BeNull();
        statistics.AverageFinalScore.Should().BeNull();
        statistics.HighestCourse.Should().BeNull();
        statistics.LowestCourse.Should().BeNull();
        statistics.StatusDistribution.Should().OnlyContain(item => item.Count == 0);
    }

    [Fact]
    public async Task GetStatisticsAsync_counts_statuses_and_finds_score_extremes()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        major.RequiredCredits = 12;
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("FA25", 1, isCurrent: true);
        var high = db.AddCourse("HIGH", credits: 3);
        var low = db.AddCourse("LOW", credits: 2);
        var current = db.AddCourse("NOW", credits: 4);

        var passed = db.AddEnrollment(
            student.StudentId,
            high.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Passed,
            9m);
        passed.LetterGrade = "A";
        var failed = db.AddEnrollment(
            student.StudentId,
            low.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Failed,
            4m);
        failed.LetterGrade = "F";
        db.AddEnrollment(
            student.StudentId,
            current.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Studying);
        db.SaveChanges();

        var statistics = await CreateAnalyticsService(db).GetStatisticsAsync(student.StudentId);

        statistics.TotalCourses.Should().Be(3);
        statistics.PassedCourses.Should().Be(1);
        statistics.FailedCourses.Should().Be(1);
        statistics.StudyingCourses.Should().Be(1);
        statistics.CompletedCredits.Should().Be(3);
        statistics.FailedCredits.Should().Be(2);
        statistics.IncompleteCredits.Should().Be(6);
        statistics.AverageFinalScore.Should().Be(6.5m);
        statistics.HighestCourse!.CourseCode.Should().Be("HIGH");
        statistics.LowestCourse!.CourseCode.Should().Be("LOW");
        statistics.ProgramProgressPercent.Should().Be(25m);
    }

    private static GradeService CreateGradeService(FAT_DBContext db)
    {
        var gpaService = new GpaService(db);
        return new GradeService(
            db,
            gpaService,
            new PrerequisiteService(db),
            TestCurrentUserContext.Admin());
    }

    private static AnalyticsService CreateAnalyticsService(FAT_DBContext db)
    {
        var gpaService = new GpaService(db);
        var gradeService = new GradeService(
            db,
            gpaService,
            new PrerequisiteService(db),
            TestCurrentUserContext.Admin());

        return new AnalyticsService(
            db,
            gpaService,
            gradeService,
            TestCurrentUserContext.Admin());
    }

    private static (
        Student Student,
        Course Course,
        Semester Semester,
        Enrollment Enrollment) AddCourseAttempt(FAT_DBContext db)
    {
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("FA25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192", credits: 3);
        var enrollment = db.AddEnrollment(
            student.StudentId,
            course.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Studying);

        return (student, course, semester, enrollment);
    }

    private static Assessment AddAssessment(
        FAT_DBContext db,
        int courseId,
        string name,
        decimal weight,
        decimal? minScore = null,
        int displayOrder = 1)
    {
        var assessment = new Assessment
        {
            CourseId = courseId,
            Name = name,
            Weight = weight,
            MinScoreToPass = minScore,
            DisplayOrder = displayOrder
        };

        db.Assessments.Add(assessment);
        db.SaveChanges();
        return assessment;
    }
}
