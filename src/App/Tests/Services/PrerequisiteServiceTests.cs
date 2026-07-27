using Domain.Enums;
using FluentAssertions;
using Services.Implementations;
using Tests.TestSupport;

namespace Tests.Services;

/// <summary>
/// Prerequisite checking, including the AND-of-ORs semantics that the flat
/// original schema could not express.
/// </summary>
public class PrerequisiteServiceTests
{
    [Fact]
    public async Task CanEnrollAsync_allows_a_subject_with_no_prerequisites()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var course = db.AddCourse("PRF192");

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, course.CourseId);

        result.CanEnroll.Should().BeTrue();
    }

    [Fact]
    public async Task CanEnrollAsync_blocks_a_subject_whose_prerequisite_is_untaken()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, pro.CourseId);

        result.CanEnroll.Should().BeFalse();
        result.Unmet.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }

    [Fact]
    public async Task CanEnrollAsync_allows_a_subject_once_its_prerequisite_is_passed()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddEnrollment(student.StudentId, prf.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 8.0m);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, pro.CourseId);

        result.CanEnroll.Should().BeTrue();
    }

    /// <summary>
    /// The point of the rule is that the earlier subject is FINISHED, so being
    /// mid-way through it is not enough.
    /// </summary>
    [Fact]
    public async Task CanEnrollAsync_does_not_accept_a_prerequisite_that_is_still_in_progress()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddEnrollment(student.StudentId, prf.CourseId, semester.SemesterId, EnrollmentStatus.Studying);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, pro.CourseId);

        result.CanEnroll.Should().BeFalse();
        result.Unmet.Single().CurrentStatus.Should().Be(EnrollmentStatus.Studying);
    }

    /// <summary>"MKT101 or MKG101" - passing either one is enough.</summary>
    [Fact]
    public async Task CanEnrollAsync_satisfies_a_choice_group_from_any_one_alternative()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("BBA");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var target = db.AddCourse("MKT205");
        var optionA = db.AddCourse("MKT101");
        var optionB = db.AddCourse("MKG101");

        db.AddPrerequisite(target.CourseId, optionA.CourseId, groupNo: 1);
        db.AddPrerequisite(target.CourseId, optionB.CourseId, groupNo: 1);

        db.AddEnrollment(student.StudentId, optionB.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 7.0m);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, target.CourseId);

        result.CanEnroll.Should().BeTrue("passing any one alternative satisfies the group");
    }

    /// <summary>With none of the alternatives passed, all of them are reported.</summary>
    [Fact]
    public async Task CanEnrollAsync_lists_every_alternative_when_a_choice_group_is_unmet()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("BBA");
        var student = db.AddStudent(major.MajorId);
        var target = db.AddCourse("MKT205");
        db.AddPrerequisite(target.CourseId, db.AddCourse("MKT101").CourseId, groupNo: 1);
        db.AddPrerequisite(target.CourseId, db.AddCourse("MKG101").CourseId, groupNo: 1);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, target.CourseId);

        result.CanEnroll.Should().BeFalse();
        result.Unmet.Should().HaveCount(2, "the student should see the whole choice");
    }

    /// <summary>"MLN111, MLN122" - two standalone rows, both required.</summary>
    [Fact]
    public async Task CanEnrollAsync_requires_every_standalone_prerequisite()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var target = db.AddCourse("HCM202");
        var a = db.AddCourse("MLN111");
        var b = db.AddCourse("MLN122");

        db.AddPrerequisite(target.CourseId, a.CourseId);
        db.AddPrerequisite(target.CourseId, b.CourseId);

        db.AddEnrollment(student.StudentId, a.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 7.0m);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, target.CourseId);

        result.CanEnroll.Should().BeFalse();
        result.Unmet.Should().ContainSingle().Which.CourseCode.Should().Be("MLN122");
    }

    /// <summary>"A, and either B or C" - the mixed shape.</summary>
    [Fact]
    public async Task CanEnrollAsync_combines_a_standalone_requirement_with_a_choice_group()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("BBA");
        var student = db.AddStudent(major.MajorId);
        var semester = db.AddSemester("SP25", 1, isCurrent: true);
        var target = db.AddCourse("TARGET1");
        var mandatory = db.AddCourse("ACC101");
        var optionA = db.AddCourse("MGT101");
        var optionB = db.AddCourse("MKG101");

        db.AddPrerequisite(target.CourseId, mandatory.CourseId);
        db.AddPrerequisite(target.CourseId, optionA.CourseId, groupNo: 1);
        db.AddPrerequisite(target.CourseId, optionB.CourseId, groupNo: 1);

        // The choice group is satisfied, the standalone one is not.
        db.AddEnrollment(student.StudentId, optionA.CourseId, semester.SemesterId,
            EnrollmentStatus.Passed, finalScore: 7.0m);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, target.CourseId);

        result.CanEnroll.Should().BeFalse();
        result.Unmet.Should().ContainSingle().Which.CourseCode.Should().Be("ACC101");
    }

    /// <summary>
    /// A subject failed once and passed later is passed. Judging it on the
    /// earlier attempt would keep the next subject locked forever.
    /// </summary>
    [Fact]
    public async Task CanEnrollAsync_judges_a_retaken_prerequisite_on_its_best_outcome()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var first = db.AddSemester("SP25", 1);
        var second = db.AddSemester("SU25", 2, isCurrent: true);
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);

        db.AddEnrollment(student.StudentId, prf.CourseId, first.SemesterId,
            EnrollmentStatus.Failed, finalScore: 3.0m, isCounted: false);
        db.AddEnrollment(student.StudentId, prf.CourseId, second.SemesterId,
            EnrollmentStatus.Passed, finalScore: 7.0m, attemptNo: 2);

        var result = await new PrerequisiteService(db).CanEnrollAsync(student.StudentId, pro.CourseId);

        result.CanEnroll.Should().BeTrue();
    }

    [Fact]
    public async Task CanEnrollManyAsync_returns_a_verdict_for_every_subject_asked_about()
    {
        using var db = TestDb.CreateWithReferenceData();
        var major = db.AddMajor("SE");
        var student = db.AddStudent(major.MajorId);
        var free = db.AddCourse("FREE101");
        var prf = db.AddCourse("PRF192");
        var locked = db.AddCourse("PRO192");
        db.AddPrerequisite(locked.CourseId, prf.CourseId);

        var results = await new PrerequisiteService(db).CanEnrollManyAsync(
            student.StudentId, [free.CourseId, locked.CourseId]);

        results.Should().HaveCount(2);
        results[free.CourseId].CanEnroll.Should().BeTrue();
        results[locked.CourseId].CanEnroll.Should().BeFalse();
    }

    [Fact]
    public async Task GetPrerequisiteTreeAsync_resolves_a_multi_level_chain()
    {
        using var db = TestDb.CreateWithReferenceData();
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        var prn = db.AddCourse("PRN212");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddPrerequisite(prn.CourseId, pro.CourseId);

        var tree = await new PrerequisiteService(db).GetPrerequisiteTreeAsync(prn.CourseId);

        tree.CourseCode.Should().Be("PRN212");
        tree.Children.Should().ContainSingle().Which.CourseCode.Should().Be("PRO192");
        tree.Children[0].Children.Should().ContainSingle().Which.CourseCode.Should().Be("PRF192");
    }

    /// <summary>
    /// Bad data can already contain a cycle - a row inserted before the guard
    /// existed. The tree must still return rather than recurse until the process
    /// dies.
    /// </summary>
    [Fact]
    public async Task GetPrerequisiteTreeAsync_terminates_on_cyclic_data()
    {
        using var db = TestDb.CreateWithReferenceData();
        var a = db.AddCourse("AAA101");
        var b = db.AddCourse("BBB101");
        db.AddPrerequisite(a.CourseId, b.CourseId);
        db.AddPrerequisite(b.CourseId, a.CourseId);

        var act = async () => await new PrerequisiteService(db).GetPrerequisiteTreeAsync(a.CourseId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDirectPrerequisitesAsync_returns_only_the_first_level()
    {
        using var db = TestDb.CreateWithReferenceData();
        var prf = db.AddCourse("PRF192");
        var pro = db.AddCourse("PRO192");
        var prn = db.AddCourse("PRN212");
        db.AddPrerequisite(pro.CourseId, prf.CourseId);
        db.AddPrerequisite(prn.CourseId, pro.CourseId);

        var direct = await new PrerequisiteService(db).GetDirectPrerequisitesAsync(prn.CourseId);

        direct.Should().ContainSingle().Which.CourseCode.Should().Be("PRO192");
    }
}
