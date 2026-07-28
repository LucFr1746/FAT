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
    public async Task GetGradesAsync_loads_only_the_assessment_metadata_needed_by_grade()
    {
        using var db = TestDb.CreateWithReferenceData();
        var setup = CreateEnrollment(db);
        var assessment = AddAssessment(
            db,
            setup.CourseId,
            "Final exam",
            1m,
            minScore: 4m);
        var service = CreateService(db, setup.StudentId);
        await service.UpsertGradeAsync(setup.EnrollmentId, assessment.AssessmentId, 8m);

        var grade = (await service.GetGradesAsync(setup.EnrollmentId)).Single();

        grade.Score.Should().Be(8m);
        grade.Assessment.Should().NotBeNull();
        grade.Assessment!.Name.Should().Be("Final exam");
        grade.Assessment.Weight.Should().Be(1m);
        grade.Assessment.MinScoreToPass.Should().Be(4m);
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
        rows[0].StatusDisplay.Should().Be("Chưa có điểm");
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
        before[0].StatusDisplay.Should().Be("Chưa có điểm");
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
        after[0].StatusDisplay.Should().Be("Đạt");
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
                                      && r.StatusDisplay == "Chưa có điểm");
        (await db.Enrollments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetTermOptionsAsync_reads_active_term_names_from_the_database()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var firstCourse = db.AddCourse("DBTERM1");
        var secondCourse = db.AddCourse("DBTERM2");
        db.AddCurriculumItem(major.MajorId, firstCourse.CourseId, termNo: 1);
        db.AddCurriculumItem(major.MajorId, secondCourse.CourseId, termNo: 2);

        (await db.Terms.SingleAsync(t => t.TermNo == 1)).TermName = "Giai đoạn nền tảng";
        (await db.Terms.SingleAsync(t => t.TermNo == 2)).IsActive = false;
        await db.SaveChangesAsync();

        var options = await CreateService(db, student.StudentId)
            .GetTermOptionsAsync(student.StudentId);

        options.Should().ContainSingle();
        options[0].TermNo.Should().Be(1);
        options[0].Display.Should().Be("Giai đoạn nền tảng");
    }

    [Fact]
    public async Task GetStudentGradesAsync_uses_the_term_name_stored_in_the_database()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("DBNAME1");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);

        (await db.Terms.SingleAsync(t => t.TermNo == 1)).TermName = "Học kỳ cơ sở";
        await db.SaveChangesAsync();

        var row = (await CreateService(db, student.StudentId)
                .GetStudentGradesAsync(student.StudentId))
            .Single();

        row.CurriculumTermDisplay.Should().Be("Học kỳ cơ sở");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Grade_screens_repair_utf8_term_zero_names_misread_as_windows1252(
        int encodingPasses)
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("UTF8101");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 0);

        const string expectedTermName = "Kỳ 0 (Định hướng)";
        System.Text.Encoding.RegisterProvider(
            System.Text.CodePagesEncodingProvider.Instance);
        var windows1252 = System.Text.Encoding.GetEncoding(1252);
        var mojibakeTermName = expectedTermName;
        for (var pass = 0; pass < encodingPasses; pass++)
        {
            mojibakeTermName = windows1252.GetString(
                System.Text.Encoding.UTF8.GetBytes(mojibakeTermName));
        }

        (await db.Terms.SingleAsync(t => t.TermNo == 0)).TermName = mojibakeTermName;
        await db.SaveChangesAsync();
        var service = CreateService(db, student.StudentId);

        var option = (await service.GetTermOptionsAsync(student.StudentId)).Single();
        var row = (await service.GetStudentGradesAsync(student.StudentId)).Single();

        option.Display.Should().Be(expectedTermName);
        row.CurriculumTermDisplay.Should().Be(expectedTermName);
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

    [Fact]
    public async Task Simplified_grade_entry_uses_the_current_database_semester()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("CURRENT1");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);
        db.AddSemester("OLD24", 1);
        var currentSemester = db.AddSemester("NOW25", 2, isCurrent: true);
        var assessment = AddAssessment(db, course.CourseId, "Final exam", 1m);

        var enrollmentId = await CreateService(db, student.StudentId)
            .UpsertStudentGradeAsync(
                student.StudentId,
                enrollmentId: 0,
                course.CourseId,
                assessment.AssessmentId,
                score: 8m);

        enrollmentId.Should().BeGreaterThan(0);
        (await db.Enrollments.SingleAsync()).SemesterId
            .Should().Be(currentSemester.SemesterId);
        (await db.Grades.SingleAsync()).Score.Should().Be(8m);
    }

    [Fact]
    public async Task Simplified_grade_entry_requires_a_current_database_semester()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor();
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("NOCURRENT");
        db.AddCurriculumItem(major.MajorId, course.CourseId, termNo: 1);
        db.AddSemester("OLD24", 1);
        var assessment = AddAssessment(db, course.CourseId, "Final exam", 1m);

        var act = () => CreateService(db, student.StudentId)
            .UpsertStudentGradeAsync(
                student.StudentId,
                enrollmentId: 0,
                course.CourseId,
                assessment.AssessmentId,
                score: 8m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*học kỳ hiện tại*");
        (await db.Enrollments.CountAsync()).Should().Be(0);
        (await db.Grades.CountAsync()).Should().Be(0);
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
