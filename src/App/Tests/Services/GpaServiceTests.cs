using Domain.Enums;
using FluentAssertions;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

/// <summary>
/// GPA and credit totals - the most depended-on calculation in the application.
///
/// Each rule from IGpaService's contract gets its own test, because a silent
/// error here is invisible: the number simply looks plausible and wrong.
/// </summary>
public class GpaServiceTests
{
    [Fact]
    public async Task GetCumulativeGpaAsync_returns_null_when_nothing_has_been_passed()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);

        var gpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        gpa.Should().BeNull("a new student has no GPA; 0.00 would read as failure");
    }

    [Fact]
    public async Task GetCumulativeGpaAsync_weights_the_average_by_credits()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        // A plain mean would give 8.0; weighted by credits it is 7.0.
        db.AddEnrollment(student.StudentId, db.AddCourse("SMALL", credits: 1).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 10.0m);
        db.AddEnrollment(student.StudentId, db.AddCourse("LARGE", credits: 4).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 6.0m);

        var gpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        gpa.Should().Be(6.80m);
    }

    [Fact]
    public async Task GetCumulativeGpaAsync_ignores_failed_and_withdrawn_and_studying_attempts()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("PASSED", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 8.0m);
        db.AddEnrollment(student.StudentId, db.AddCourse("FAILED", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Failed, 3.0m);
        db.AddEnrollment(student.StudentId, db.AddCourse("QUIT", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Withdrawn);
        db.AddEnrollment(student.StudentId, db.AddCourse("DOING", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Studying);

        var gpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        gpa.Should().Be(8.00m, "only passed attempts count, on neither side of the fraction");
    }

    /// <summary>
    /// The classic bug: ignoring IsCounted averages a retaken subject twice and
    /// produces a suspiciously high GPA.
    /// </summary>
    [Fact]
    public async Task GetCumulativeGpaAsync_counts_a_retaken_subject_once()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var first = db.AddSemester("SP25", 1);
        var second = db.AddSemester("SU25", 2, isCurrent: true);
        var course = db.AddCourse("PRF192", credits: 3);

        db.AddEnrollment(student.StudentId, course.CourseId, first.SemesterId,
            EnrollmentStatus.Passed, 5.0m, isCounted: false);
        db.AddEnrollment(student.StudentId, course.CourseId, second.SemesterId,
            EnrollmentStatus.Passed, 9.0m, attemptNo: 2);

        var gpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        gpa.Should().Be(9.00m, "only the latest attempt counts");
    }

    /// <summary>The rule the FLM catalog forced: 33 of 135 subjects are "Tính GPA = Không".</summary>
    [Fact]
    public async Task GetCumulativeGpaAsync_excludes_subjects_that_do_not_count_toward_gpa()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("MATH", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 7.0m);
        db.AddEnrollment(student.StudentId,
            db.AddCourse("VOV114", credits: 2, countsTowardGpa: false).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 10.0m);

        var gpa = await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        gpa.Should().Be(7.00m, "physical education must not lift the GPA");
    }

    /// <summary>...but its credits are still earned.</summary>
    [Fact]
    public async Task GetCreditSummaryAsync_counts_credits_from_non_gpa_subjects()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("MATH", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 7.0m);
        db.AddEnrollment(student.StudentId,
            db.AddCourse("VOV114", credits: 2, countsTowardGpa: false).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 10.0m);
        db.AddEnrollment(student.StudentId, db.AddCourse("DOING", credits: 4).CourseId,
            semester.SemesterId, EnrollmentStatus.Studying);
        db.AddEnrollment(student.StudentId, db.AddCourse("FAILED", credits: 2).CourseId,
            semester.SemesterId, EnrollmentStatus.Failed, 3.0m);

        var summary = await new GpaService(db).GetCreditSummaryAsync(student.StudentId);

        summary.EarnedCredits.Should().Be(5);
        summary.InProgressCredits.Should().Be(4);
        summary.FailedCredits.Should().Be(2);
    }

    [Fact]
    public async Task GetGpaBySemesterAsync_orders_by_chronology_not_by_code()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);

        // "FA25" sorts before "SP26" alphabetically but happens first in time.
        var fa25 = db.AddSemester("FA25", 1);
        var sp26 = db.AddSemester("SP26", 2, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("AAA101", credits: 3).CourseId,
            fa25.SemesterId, EnrollmentStatus.Passed, 7.0m);
        db.AddEnrollment(student.StudentId, db.AddCourse("BBB101", credits: 3).CourseId,
            sp26.SemesterId, EnrollmentStatus.Passed, 9.0m);

        var bySemester = await new GpaService(db).GetGpaBySemesterAsync(student.StudentId);

        bySemester.Select(s => s.SemesterCode).Should().ContainInOrder("FA25", "SP26");
    }

    [Fact]
    public async Task GetGpaSummaryAsync_classifies_the_cumulative_gpa()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("AAA101", credits: 3).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 8.5m);

        var summary = await new GpaService(db).GetGpaSummaryAsync(student.StudentId);

        summary.CumulativeGpa.Should().Be(8.50m);
        summary.Classification.Should().Be(DegreeClassification.VeryGood);
        summary.ClassificationName.Should().Be("Very Good");
    }

    /// <summary>
    /// A zero-credit subject (the Kỳ 0 orientation block) must not make the
    /// weighted average divide by zero.
    /// </summary>
    [Fact]
    public async Task GetCumulativeGpaAsync_survives_a_transcript_of_only_zero_credit_subjects()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        db.AddEnrollment(student.StudentId, db.AddCourse("OTP101", credits: 0).CourseId,
            semester.SemesterId, EnrollmentStatus.Passed, 8.0m);

        var act = async () => await new GpaService(db).GetCumulativeGpaAsync(student.StudentId);

        await act.Should().NotThrowAsync();
        (await act()).Should().BeNull();
    }
}
