using Data;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Services.Dtos;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

/// <summary>
/// GPA forecasting and the retake penalty.
///
/// The most valuable case here is the cross-check: predicting with NO planned
/// scores must reproduce the student's real GPA exactly. That is the cheapest
/// way to catch this service drifting away from GpaService, and a drift would
/// only show at a boundary where nobody looks until it matters.
/// </summary>
public class GpaPredictionServiceTests
{
    private static GpaPredictionService CreateService(FAT_DBContext db, int studentId)
        => new(db, TestCurrentUserContext.Student(studentId), new GpaService(db));

    /// <summary>
    /// A student with three passed subjects: 3cr@8.0, 3cr@9.0, 4cr@7.0
    /// -> (24 + 27 + 28) / 10 = 7.90
    /// </summary>
    private static (Domain.Entities.Student Student, Domain.Entities.Semester Semester) SeedPassedStudent(
        FAT_DBContext db)
    {
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        var a = db.AddCourse("AAA101", credits: 3);
        var b = db.AddCourse("BBB101", credits: 3);
        var c = db.AddCourse("CCC101", credits: 4);

        db.AddCurriculumItem(major.MajorId, a.CourseId, 1);
        db.AddCurriculumItem(major.MajorId, b.CourseId, 1);
        db.AddCurriculumItem(major.MajorId, c.CourseId, 1);

        db.AddEnrollment(student.StudentId, a.CourseId, semester.SemesterId, EnrollmentStatus.Passed, 8.0m);
        db.AddEnrollment(student.StudentId, b.CourseId, semester.SemesterId, EnrollmentStatus.Passed, 9.0m);
        db.AddEnrollment(student.StudentId, c.CourseId, semester.SemesterId, EnrollmentStatus.Passed, 7.0m);

        return (student, semester);
    }

    /// <summary>THE cross-check against the real GPA.</summary>
    [Fact]
    public async Task PredictAsync_with_no_planned_scores_reproduces_the_real_gpa()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);

        var realGpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);
        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.PredictedGpa.Should().Be(realGpa!.Value);
        prediction.CurrentGpa.Should().Be(realGpa.Value);
        prediction.PredictedGpa.Should().Be(7.90m);
    }

    [Fact]
    public async Task PredictAsync_folds_planned_scores_into_the_projection()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var planned = db.AddCourse("DDD101", credits: 2);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(
            student.StudentId, [new PlannedGradeDto(planned.CourseId, 10.0m)]);

        // (24 + 27 + 28 + 20) / 12 = 8.25
        prediction.PredictedGpa.Should().Be(8.25m);
        prediction.PredictedGpa.Should().BeGreaterThan(prediction.CurrentGpa!.Value);
    }

    /// <summary>
    /// A planned score below the pass mark earns no credits but still enters
    /// the GPA, matching a real completed Failed result.
    /// </summary>
    [Fact]
    public async Task PredictAsync_includes_a_planned_score_below_the_pass_mark()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var planned = db.AddCourse("DDD101", credits: 3);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(
            student.StudentId, [new PlannedGradeDto(planned.CourseId, 4.0m)]);

        // Existing weighted total 79 plus 4*3, over 10+3 credits = 7.00.
        prediction.PredictedGpa.Should().Be(7.00m);
        prediction.ProjectedEarnedCredits.Should().Be(10);
    }

    /// <summary>
    /// Physical education and the orientation block earn credits but must not
    /// move the GPA.
    /// </summary>
    [Fact]
    public async Task PredictAsync_excludes_non_gpa_subjects_from_the_average()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var physicalEducation = db.AddCourse("VOV114", credits: 2, countsTowardGpa: false);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(
            student.StudentId, [new PlannedGradeDto(physicalEducation.CourseId, 10.0m)]);

        prediction.PredictedGpa.Should().Be(7.90m, "a non-GPA subject cannot lift the average");
    }

    /// <summary>...but it does still earn its credits.</summary>
    [Fact]
    public async Task PredictAsync_still_counts_the_credits_of_a_non_gpa_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var physicalEducation = db.AddCourse("VOV114", credits: 2, countsTowardGpa: false);

        var baseline = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);
        var withPe = await CreateService(db, student.StudentId).PredictAsync(
            student.StudentId, [new PlannedGradeDto(physicalEducation.CourseId, 8.0m)]);

        withPe.ProjectedEarnedCredits.Should().Be(baseline.ProjectedEarnedCredits + 2);
    }

    // =========================================================================
    // The retake penalty
    // =========================================================================

    [Fact]
    public async Task PredictAsync_leaves_the_classification_alone_below_three_retakes()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        AddRetakenSubjects(db, student.StudentId, count: 2);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.RetakenSubjectCount.Should().Be(2);
        prediction.IsDemoted.Should().BeFalse();
        prediction.AdjustedClassification.Should().Be(prediction.BaseClassification);
        prediction.DemotionReason.Should().BeNull();
    }

    [Fact]
    public async Task PredictAsync_drops_exactly_one_rank_at_three_retakes()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        AddRetakenSubjects(db, student.StudentId, count: 3);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.RetakenSubjectCount.Should().Be(3);
        prediction.IsDemoted.Should().BeTrue();
        // 7.90 is Good on its own; the penalty takes it to Fairly Good.
        prediction.BaseClassification.Should().Be(DegreeClassification.Good);
        prediction.AdjustedClassification.Should().Be(DegreeClassification.FairGood);
        prediction.DemotionReason.Should().NotBeNull();
    }

    /// <summary>The penalty is capped: more retakes do not keep pushing it down.</summary>
    [Fact]
    public async Task PredictAsync_never_drops_more_than_one_rank()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        AddRetakenSubjects(db, student.StudentId, count: 8);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.BaseClassification.Should().Be(DegreeClassification.Good);
        prediction.AdjustedClassification.Should().Be(DegreeClassification.FairGood);
    }

    /// <summary>
    /// The counter counts DISTINCT zero-score subjects, not enrollment rows.
    /// </summary>
    [Fact]
    public async Task PredictAsync_counts_distinct_zero_score_subjects()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);

        var course = db.AddCourse("RETAKE1", credits: 3);
        var s2 = db.AddSemester("SU25", 2);
        var s3 = db.AddSemester("FA25", 3);

        db.AddEnrollment(student.StudentId, course.CourseId, s2.SemesterId,
            EnrollmentStatus.Failed, 0m, isCounted: false, attemptNo: 1);
        db.AddEnrollment(student.StudentId, course.CourseId, s3.SemesterId,
            EnrollmentStatus.Failed, 0m, isCounted: true, attemptNo: 2);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.RetakenSubjectCount.Should().Be(1, "two rows for one zero-score subject count once");
        prediction.IsDemoted.Should().BeFalse();
    }

    [Fact]
    public async Task PredictAsync_does_not_treat_a_nonzero_failed_score_as_a_retake()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var course = db.AddCourse("FAILED4", credits: 3);
        var semester = db.AddSemester("SU25", 2);

        db.AddEnrollment(student.StudentId, course.CourseId, semester.SemesterId,
            EnrollmentStatus.Failed, 4.0m, isCounted: true, attemptNo: 2);

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.RetakenSubjectCount.Should().Be(0,
            "the agreed prediction rule counts only subjects whose final score is exactly zero");
    }

    [Fact]
    public async Task PredictAsync_counts_every_complete_zero_score_subject_for_a_registered_student()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        student.CurrentTermNo = 1;
        var semester = db.AddSemester("SP24", 1);

        foreach (var code in new[] { "ZERO01", "ZERO02" })
        {
            var course = db.AddCourse(code, credits: 3);
            var assessment = new Domain.Entities.Assessment
            {
                CourseId = course.CourseId,
                Name = "Final Exam",
                Weight = 1m,
                DisplayOrder = 1
            };
            db.Assessments.Add(assessment);
            db.SaveChanges();

            var enrollment = db.AddEnrollment(
                student.StudentId,
                course.CourseId,
                semester.SemesterId,
                EnrollmentStatus.Failed,
                finalScore: 0m);
            db.Grades.Add(new Domain.Entities.Grade
            {
                EnrollmentId = enrollment.EnrollmentId,
                AssessmentId = assessment.AssessmentId,
                Score = 0m,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // A zero aggregate without its component grade is still only a
        // placeholder and must not become a third retake.
        var ungraded = db.AddCourse("UNGRD0", credits: 3);
        db.Assessments.Add(new Domain.Entities.Assessment
        {
            CourseId = ungraded.CourseId,
            Name = "Final Exam",
            Weight = 1m,
            DisplayOrder = 1
        });
        db.SaveChanges();
        db.AddEnrollment(
            student.StudentId,
            ungraded.CourseId,
            semester.SemesterId,
            EnrollmentStatus.Failed,
            finalScore: 0m);
        db.SaveChanges();

        var prediction = await CreateService(db, student.StudentId).PredictAsync(student.StudentId);

        prediction.RetakenSubjectCount.Should().Be(2);
    }

    // =========================================================================
    // Snapshots
    // =========================================================================

    [Fact]
    public async Task SaveSnapshotAsync_stores_the_forecast()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var service = CreateService(db, student.StudentId);

        var prediction = await service.PredictAsync(student.StudentId);
        await service.SaveSnapshotAsync(student.StudentId, prediction, "Kiểm tra");

        var stored = await db.GradePredictions.SingleAsync();
        stored.PredictedGpa.Should().Be(prediction.PredictedGpa);
        stored.BaseClassification.Should().Be(prediction.BaseClassification);
        stored.Note.Should().Be("Kiểm tra");
    }

    [Fact]
    public async Task GetHistoryAsync_returns_the_newest_snapshot_first()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var service = CreateService(db, student.StudentId);
        var prediction = await service.PredictAsync(student.StudentId);

        await service.SaveSnapshotAsync(student.StudentId, prediction, "first");
        await Task.Delay(10);
        await service.SaveSnapshotAsync(student.StudentId, prediction, "second");

        var history = await service.GetHistoryAsync(student.StudentId);

        history.Should().HaveCount(2);
        history[0].DemotionReason.Should().Be("second");
    }

    [Fact]
    public async Task PredictAsync_refuses_to_forecast_for_another_student()
    {
        using var db = TestDb.CreateWithReferenceData();
        var (student, _) = SeedPassedStudent(db);
        var other = db.AddStudent(student.MajorId, "SE000099");

        var act = () => CreateService(db, student.StudentId).PredictAsync(other.StudentId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    /// <summary>
    /// Adds distinct current subjects with a zero final score. These fixtures
    /// do not count toward GPA so the classification-penalty tests isolate that
    /// rule; GPA tests cover zero-score courses with normal credits separately.
    /// </summary>
    private static void AddRetakenSubjects(FAT_DBContext db, int studentId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var course = db.AddCourse($"RTK{i:D3}", credits: 3, countsTowardGpa: false);
            var semester = db.AddSemester($"RS{i:D2}", 10 + i);

            db.AddEnrollment(studentId, course.CourseId, semester.SemesterId,
                EnrollmentStatus.Failed, finalScore: 0m, isCounted: true);
        }
    }
}
