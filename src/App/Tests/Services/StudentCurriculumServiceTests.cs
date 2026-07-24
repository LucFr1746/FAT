using Data;
using Domain.Enums;
using Services.Abstractions;
using Services.Implementations;
using Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Tests.Services;

/// <summary>
/// The student workflow and the retake flow.
///
/// The two behaviours worth guarding hardest:
///   - a locked subject is REMOVED from the list, not greyed out;
///   - a retake creates a new attempt and demotes the old ones, so the GPA
///     counts the subject exactly once.
/// </summary>
public class StudentCurriculumServiceTests
{
    private static StudentCurriculumService CreateService(
        FAT_DBContext db, int studentId, bool asAdmin = false)
    {
        ICurrentUserContext user = asAdmin
            ? TestCurrentUserContext.Admin()
            : TestCurrentUserContext.Student(studentId);

        return new StudentCurriculumService(db, user, new PrerequisiteService(db));
    }

    // =========================================================================
    // Prerequisite hiding
    // =========================================================================

    [Fact]
    public async Task GetTermCurriculumAsync_lists_the_subjects_of_the_chosen_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("PRF192").CourseId, 1);
        db.AddCurriculumItem(major.MajorId, db.AddCourse("CSD201").CourseId, 3);

        var result = await CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(student.StudentId, termNo: 1);

        result.Subjects.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }

    /// <summary>
    /// THE RULE: hide, do not disable. The locked subject must be absent from
    /// the list entirely.
    /// </summary>
    [Fact]
    public async Task GetTermCurriculumAsync_hides_a_subject_with_an_unmet_prerequisite()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);

        db.AddCurriculumItem(major.MajorId, prf.CourseId, 2);
        db.AddCurriculumItem(major.MajorId, pro.CourseId, 2);

        var result = await CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(student.StudentId, termNo: 2);

        result.Subjects.Select(s => s.CourseCode).Should().NotContain("PRO192");
        result.Subjects.Select(s => s.CourseCode).Should().Contain("PRF192");
    }

    /// <summary>
    /// A silently shorter list looks like missing data, so what was hidden is
    /// counted and named.
    /// </summary>
    [Fact]
    public async Task GetTermCurriculumAsync_reports_what_it_hid()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddCurriculumItem(major.MajorId, pro.CourseId, 2);

        var result = await CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(student.StudentId, termNo: 2);

        result.HiddenByPrerequisiteCount.Should().Be(1);
        result.HiddenSubjectCodes.Should().Contain("PRO192");
        result.HasHiddenSubjects.Should().BeTrue();
    }

    [Fact]
    public async Task GetTermCurriculumAsync_reveals_the_subject_once_its_prerequisite_is_passed()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddCurriculumItem(major.MajorId, pro.CourseId, 2);

        db.AddEnrollment(student.StudentId, prf.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 8.0m);

        var result = await CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(student.StudentId, termNo: 2);

        result.Subjects.Select(s => s.CourseCode).Should().Contain("PRO192");
        result.HiddenByPrerequisiteCount.Should().Be(0);
    }

    /// <summary>
    /// A subject the student is already sitting stays visible even if the rule
    /// would hide it - removing their own history is more confusing than the
    /// lock it expresses.
    /// </summary>
    [Fact]
    public async Task GetTermCurriculumAsync_keeps_a_subject_the_student_has_already_started()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddCurriculumItem(major.MajorId, pro.CourseId, 2);

        db.AddEnrollment(student.StudentId, pro.CourseId, semester.SemesterId, EnrollmentStatus.Studying);

        var result = await CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(student.StudentId, termNo: 2);

        result.Subjects.Select(s => s.CourseCode).Should().Contain("PRO192");
    }

    [Fact]
    public async Task GetTermCurriculumAsync_refuses_to_show_another_students_curriculum()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId, "SE000001");
        var other = db.AddStudent(major.MajorId, "SE000002");

        var act = () => CreateService(db, student.StudentId)
            .GetTermCurriculumAsync(other.StudentId, termNo: 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // =========================================================================
    // Major and term selection
    // =========================================================================

    [Fact]
    public async Task SetCurrentTermAsync_writes_both_the_number_and_its_display_text()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);

        await CreateService(db, student.StudentId).SetCurrentTermAsync(student.StudentId, 5);

        var updated = await db.Students.SingleAsync();
        updated.CurrentTermNo.Should().Be(5);
        updated.CurrentSemester.Should().Be("Kỳ 5", "the Profile screen binds to the text form");
    }

    /// <summary>The old kỳ belongs to the old study path and means nothing in the new one.</summary>
    [Fact]
    public async Task SetMajorAsync_clears_the_current_term()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var ai = db.AddMajor("AI");
        var student = db.AddStudent(se.MajorId);

        var service = CreateService(db, student.StudentId);
        await service.SetCurrentTermAsync(student.StudentId, 5);
        await service.SetMajorAsync(student.StudentId, ai.MajorId);

        var updated = await db.Students.SingleAsync();
        updated.MajorId.Should().Be(ai.MajorId);
        updated.CurrentTermNo.Should().BeNull();
    }

    [Fact]
    public async Task SetMajorAsync_rejects_a_deactivated_major()
    {
        using var db = TestDb.CreateWithReferenceData();
        var se = db.AddMajor("SE");
        var retired = db.AddMajor("OLD");
        retired.IsActive = false;
        await db.SaveChangesAsync();

        var student = db.AddStudent(se.MajorId);

        var act = () => CreateService(db, student.StudentId).SetMajorAsync(student.StudentId, retired.MajorId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*không còn hoạt động*");
    }

    // =========================================================================
    // Retake
    // =========================================================================

    [Fact]
    public async Task GetRetakeCandidatesAsync_lists_only_subjects_that_are_still_failed()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);

        var failed = db.AddCourse("PRF192");
        var passed = db.AddCourse("MAE101");
        db.AddEnrollment(student.StudentId, failed.CourseId, semester.SemesterId,
            EnrollmentStatus.Failed, finalScore: 3.5m);
        db.AddEnrollment(student.StudentId, passed.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 8.0m);

        var candidates = await CreateService(db, student.StudentId)
            .GetRetakeCandidatesAsync(student.StudentId);

        candidates.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }

    /// <summary>Failed once, passed later: finished, not a candidate.</summary>
    [Fact]
    public async Task GetRetakeCandidatesAsync_excludes_a_subject_already_passed_on_a_later_attempt()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var first = db.AddSemester("SP25", 1);
        var second = db.AddSemester("SU25", 2, isCurrent: true);
        var course = db.AddCourse("PRF192");

        db.AddEnrollment(student.StudentId, course.CourseId, first.SemesterId,
            EnrollmentStatus.Failed, finalScore: 3.0m, isCounted: false);
        db.AddEnrollment(student.StudentId, course.CourseId, second.SemesterId,
            EnrollmentStatus.Passed, finalScore: 7.0m, attemptNo: 2);

        var candidates = await CreateService(db, student.StudentId)
            .GetRetakeCandidatesAsync(student.StudentId);

        candidates.Should().BeEmpty();
    }

    /// <summary>
    /// The core of the retake contract: a NEW enrollment, no new subject, and
    /// every earlier attempt excluded from the GPA.
    /// </summary>
    [Fact]
    public async Task AddRetakeAsync_creates_a_new_attempt_and_demotes_the_previous_ones()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var failedIn = db.AddSemester("SP25", 1);
        var retakeIn = db.AddSemester("SU25", 2, isCurrent: true);
        var course = db.AddCourse("PRF192");

        db.AddEnrollment(student.StudentId, course.CourseId, failedIn.SemesterId,
            EnrollmentStatus.Failed, finalScore: 3.0m);

        var courseCountBefore = await db.Courses.CountAsync();

        await CreateService(db, student.StudentId)
            .AddRetakeAsync(student.StudentId, course.CourseId, retakeIn.SemesterId);

        (await db.Courses.CountAsync()).Should().Be(courseCountBefore, "a retake creates no new subject");

        var attempts = await db.Enrollments
            .Where(e => e.CourseId == course.CourseId)
            .OrderBy(e => e.AttemptNo)
            .ToListAsync();

        attempts.Should().HaveCount(2);
        attempts[0].AttemptNo.Should().Be(1);
        attempts[0].IsCounted.Should().BeFalse("only the newest attempt may count toward the GPA");
        attempts[1].AttemptNo.Should().Be(2);
        attempts[1].IsCounted.Should().BeTrue();
        attempts[1].Status.Should().Be(EnrollmentStatus.Studying);
    }

    [Fact]
    public async Task AddRetakeAsync_allows_several_different_subjects_in_one_semester()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var failedIn = db.AddSemester("SP25", 1);
        var retakeIn = db.AddSemester("SU25", 2, isCurrent: true);

        var a = db.AddCourse("PRF192");
        var b = db.AddCourse("MAE101");
        db.AddEnrollment(student.StudentId, a.CourseId, failedIn.SemesterId, EnrollmentStatus.Failed, 3.0m);
        db.AddEnrollment(student.StudentId, b.CourseId, failedIn.SemesterId, EnrollmentStatus.Failed, 2.0m);

        var service = CreateService(db, student.StudentId);
        await service.AddRetakeAsync(student.StudentId, a.CourseId, retakeIn.SemesterId);
        await service.AddRetakeAsync(student.StudentId, b.CourseId, retakeIn.SemesterId);

        (await db.Enrollments.CountAsync(e => e.SemesterId == retakeIn.SemesterId)).Should().Be(2);
    }

    [Fact]
    public async Task AddRetakeAsync_refuses_a_subject_that_was_never_taken()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, student.StudentId)
            .AddRetakeAsync(student.StudentId, course.CourseId, semester.SemesterId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*chưa từng đăng ký*");
    }

    [Fact]
    public async Task AddRetakeAsync_refuses_a_subject_already_passed()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var first = db.AddSemester("SP25", 1);
        var second = db.AddSemester("SU25", 2, isCurrent: true);
        var course = db.AddCourse("PRF192");
        db.AddEnrollment(student.StudentId, course.CourseId, first.SemesterId,
            EnrollmentStatus.Passed, finalScore: 8.0m);

        var act = () => CreateService(db, student.StudentId)
            .AddRetakeAsync(student.StudentId, course.CourseId, second.SemesterId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*đã qua môn*");
    }

    /// <summary>
    /// UQ_Enrollment_Unique is on (Student, Course, Semester), so retaking in the
    /// SAME semester as the failure is impossible. The message says so instead of
    /// letting a unique-index violation surface.
    /// </summary>
    [Fact]
    public async Task AddRetakeAsync_refuses_a_retake_in_the_semester_the_subject_was_failed_in()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192");
        db.AddEnrollment(student.StudentId, course.CourseId, semester.SemesterId,
            EnrollmentStatus.Failed, finalScore: 3.0m);

        var act = () => CreateService(db, student.StudentId)
            .AddRetakeAsync(student.StudentId, course.CourseId, semester.SemesterId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*học kỳ khác*");
    }

    [Fact]
    public async Task AddRetakeAsync_refuses_to_act_for_another_student()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId, "SE000001");
        var other = db.AddStudent(major.MajorId, "SE000002");
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var course = db.AddCourse("PRF192");

        var act = () => CreateService(db, student.StudentId)
            .AddRetakeAsync(other.StudentId, course.CourseId, semester.SemesterId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // =========================================================================
    // Subject detail
    // =========================================================================

    [Fact]
    public async Task GetSubjectDetailAsync_returns_the_grade_structure_and_its_validation()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("PRF192");
        db.AddCurriculumItem(major.MajorId, course.CourseId, 1);

        db.Assessments.AddRange(
            new Domain.Entities.Assessment
            {
                CourseId = course.CourseId,
                Name = "Assignment",
                Weight = 0.40m,
                DisplayOrder = 0
            },
            new Domain.Entities.Assessment
            {
                CourseId = course.CourseId,
                Name = "Final exam",
                Weight = 0.60m,
                DisplayOrder = 1
            });
        await db.SaveChangesAsync();

        var detail = await CreateService(db, student.StudentId)
            .GetSubjectDetailAsync(student.StudentId, course.CourseId);

        detail.Should().NotBeNull();
        detail!.GradeStructure.Should().HaveCount(2);
        detail.GradeStructureValidation.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetSubjectDetailAsync_returns_null_for_an_unknown_subject()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);

        var detail = await CreateService(db, student.StudentId)
            .GetSubjectDetailAsync(student.StudentId, courseId: 9999);

        detail.Should().BeNull();
    }
}
