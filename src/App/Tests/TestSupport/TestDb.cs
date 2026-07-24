using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tests.TestSupport;

/// <summary>
/// Builds in-memory <see cref="FAT_DBContext"/> instances for unit tests.
///
/// KNOWN LIMIT OF THE IN-MEMORY PROVIDER: it does not enforce unique indexes,
/// foreign keys or CHECK constraints, and it has no transactions. That is
/// precisely why every rule the schema enforces is ALSO enforced in the service
/// layer - and it is those C# checks these tests exercise. Constraint behaviour
/// itself is covered by the SQL Server integration tests.
/// </summary>
public static class TestDb
{
    /// <summary>A fresh, empty database with a name unique to this test.</summary>
    public static FAT_DBContext Create()
    {
        var options = new DbContextOptionsBuilder<FAT_DBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The provider warns that transactions are ignored. The import
            // deliberately asks for one and skips it on a non-relational
            // provider, so the warning is expected and must not fail the test.
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new FAT_DBContext(options);
    }

    /// <summary>A database holding the roles, terms and grade scale every test needs.</summary>
    public static FAT_DBContext CreateWithReferenceData()
    {
        var db = Create();

        db.Roles.AddRange(
            new Role { RoleId = 1, RoleName = RoleNames.Admin, Description = "Admin" },
            new Role { RoleId = 2, RoleName = RoleNames.Student, Description = "Student" });

        for (var termNo = 0; termNo <= 9; termNo++)
        {
            db.Terms.Add(new Term
            {
                TermId = termNo + 1,
                TermNo = termNo,
                TermName = CatalogRules.GetTermName(termNo),
                IsActive = true
            });
        }

        db.GradeScales.AddRange(
            new GradeScale { GradeScaleId = 1, MinScore = 8.50m, MaxScore = 10.01m, LetterGrade = "A", GradePoint = 4.00m },
            new GradeScale { GradeScaleId = 2, MinScore = 7.00m, MaxScore = 8.50m, LetterGrade = "B", GradePoint = 3.00m },
            new GradeScale { GradeScaleId = 3, MinScore = 5.50m, MaxScore = 7.00m, LetterGrade = "C", GradePoint = 2.00m },
            new GradeScale { GradeScaleId = 4, MinScore = 5.00m, MaxScore = 5.50m, LetterGrade = "D", GradePoint = 1.00m },
            new GradeScale { GradeScaleId = 5, MinScore = 0.00m, MaxScore = 5.00m, LetterGrade = "F", GradePoint = 0.00m });

        db.SaveChanges();
        return db;
    }

    /// <summary>Adds a major and returns it.</summary>
    public static Major AddMajor(this FAT_DBContext db, string code = "SE", string? name = null)
    {
        var major = new Major
        {
            MajorCode = code,
            MajorName = name ?? $"Major {code}",
            RequiredCredits = 1,
            TotalTerms = 1,
            IsActive = true
        };

        db.Majors.Add(major);
        db.SaveChanges();
        return major;
    }

    /// <summary>Adds a course and returns it.</summary>
    public static Course AddCourse(
        this FAT_DBContext db, string code, int credits = 3, bool countsTowardGpa = true)
    {
        var course = new Course
        {
            CourseCode = code,
            CourseName = $"Course {code}",
            Credits = credits,
            CountsTowardGpa = countsTowardGpa,
            IsActive = true
        };

        db.Courses.Add(course);
        db.SaveChanges();
        return course;
    }

    /// <summary>Places a course into a major's study path and returns the link.</summary>
    public static Curriculum AddCurriculumItem(
        this FAT_DBContext db, int majorId, int courseId, int termNo, int displayOrder = 0)
    {
        var item = new Curriculum
        {
            MajorId = majorId,
            CourseId = courseId,
            TermNo = termNo,
            DisplayOrder = displayOrder,
            IsMandatory = true
        };

        db.CurriculumItems.Add(item);
        db.SaveChanges();
        return item;
    }

    /// <summary>Adds a semester (a calendar term) and returns it.</summary>
    public static Semester AddSemester(
        this FAT_DBContext db, string code, int displayOrder, bool isCurrent = false)
    {
        var semester = new Semester
        {
            SemesterCode = code,
            SemesterName = $"Semester {code}",
            StartDate = new DateTime(2024, 1, 1).AddMonths(4 * displayOrder),
            EndDate = new DateTime(2024, 4, 1).AddMonths(4 * displayOrder),
            DisplayOrder = displayOrder,
            IsCurrent = isCurrent
        };

        db.Semesters.Add(semester);
        db.SaveChanges();
        return semester;
    }

    /// <summary>Adds a student together with the login account it hangs off.</summary>
    public static Student AddStudent(this FAT_DBContext db, int majorId, string code = "SE000001")
    {
        var user = new AppUser
        {
            Username = code,
            PasswordHash = "not-a-real-hash",
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.SaveChanges();

        var student = new Student
        {
            UserId = user.UserId,
            StudentCode = code,
            FullName = $"Student {code}",
            EnrollmentDate = new DateTime(2024, 1, 1),
            MajorId = majorId,
            Status = StudentStatus.Active
        };

        db.Students.Add(student);
        db.SaveChanges();
        return student;
    }

    /// <summary>Records an attempt at a course, with its outcome.</summary>
    public static Enrollment AddEnrollment(
        this FAT_DBContext db,
        int studentId,
        int courseId,
        int semesterId,
        EnrollmentStatus status,
        decimal? finalScore = null,
        bool isCounted = true,
        int attemptNo = 1)
    {
        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            SemesterId = semesterId,
            Status = status,
            FinalScore = finalScore,
            IsCounted = isCounted,
            AttemptNo = attemptNo,
            CreatedAt = DateTime.UtcNow
        };

        db.Enrollments.Add(enrollment);
        db.SaveChanges();
        return enrollment;
    }

    /// <summary>Adds a prerequisite edge. A shared GroupNo above zero means "any one of".</summary>
    public static Prerequisite AddPrerequisite(
        this FAT_DBContext db, int courseId, int requiredCourseId, int groupNo = 0)
    {
        var prerequisite = new Prerequisite
        {
            CourseId = courseId,
            RequiredCourseId = requiredCourseId,
            Type = PrerequisiteType.Prerequisite,
            GroupNo = groupNo
        };

        db.Prerequisites.Add(prerequisite);
        db.SaveChanges();
        return prerequisite;
    }
}
