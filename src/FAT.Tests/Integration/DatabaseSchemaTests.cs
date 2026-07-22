using FAT.Data;
using FAT.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Integration;

/// <summary>
/// Verifies that the EF model matches the real database produced by
/// db/01_schema.sql.
///
/// WHY THIS MATTERS: the project does not use Migrations, so NOTHING otherwise
/// guarantees that the C# entities and the SQL tables stay in step. Misspell a
/// column name and the build is still green - the failure only surfaces when
/// someone opens the screen, which is usually during the demo. Each query below
/// forces EF to generate real SQL for one mapping, so any drift is caught here.
///
/// Run db/setup-db.ps1 first. When the database is unreachable these tests SKIP
/// rather than FAIL, so CI and machines without SQL Server can still run the
/// rest of the suite.
/// </summary>
public class DatabaseSchemaTests : IDisposable
{
    private const string ConnectionString =
        "Server=localhost;Database=FAT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=5";

    private readonly FatDbContext? _db;
    private readonly bool _available;

    public DatabaseSchemaTests()
    {
        var options = new DbContextOptionsBuilder<FatDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        try
        {
            _db = new FatDbContext(options);
            _available = _db.Database.CanConnect();
        }
        catch
        {
            _available = false;
        }
    }

    public void Dispose()
    {
        _db?.Dispose();
        GC.SuppressFinalize(this);
    }

    private FatDbContext RequireDb()
    {
        Skip.IfNot(_available, "SQL Server is unreachable - run db/setup-db.ps1 first.");
        return _db!;
    }

    [SkippableFact]
    public async Task Every_DbSet_queries_successfully_against_the_real_schema()
    {
        var db = RequireDb();

        // Take(1) on each DbSet forces EF to emit a real SELECT for EVERY
        // mapping. A wrong table or column name throws right here.
        (await db.Roles.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Users.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Majors.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Students.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Courses.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Prerequisites.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.CurriculumItems.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Semesters.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Enrollments.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Assessments.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Grades.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.GradeScales.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AcademicPlans.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AcademicPlanItems.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AuditLogs.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Materials.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.MaterialFiles.Take(1).ToListAsync()).Should().NotBeNull();
    }

    [SkippableFact]
    public async Task Seed_data_has_the_expected_row_counts()
    {
        var db = RequireDb();

        (await db.Courses.CountAsync()).Should().Be(31);
        (await db.Students.CountAsync()).Should().Be(3);
        (await db.Semesters.CountAsync()).Should().Be(10);
        (await db.GradeScales.CountAsync()).Should().Be(8);
        (await db.Prerequisites.CountAsync()).Should().Be(19);
    }

    [SkippableFact]
    public async Task Enums_stored_as_text_round_trip_correctly()
    {
        var db = RequireDb();

        // Filtering by an enum requires EF to translate EnrollmentStatus.Passed
        // into the string 'Passed'. A wrong HasConversion setup yields zero rows.
        var passed = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Passed)
            .CountAsync();

        passed.Should().BeGreaterThan(0, "the seed contains many completed courses");

        var studying = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Studying)
            .CountAsync();

        studying.Should().BeGreaterThan(0, "the seed contains in-progress courses for the current term");
    }

    [SkippableFact]
    public async Task Navigation_properties_join_across_multiple_tables()
    {
        var db = RequireDb();

        // A four-table join: Enrollment -> Student, Course, Semester.
        // A misdeclared foreign key in the configuration surfaces immediately.
        var row = await db.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Include(e => e.Semester)
            .Where(e => e.Student!.StudentCode == "SE170001")
            .OrderBy(e => e.Semester!.DisplayOrder)
            .FirstOrDefaultAsync();

        row.Should().NotBeNull();
        row!.Course!.CourseCode.Should().NotBeNullOrWhiteSpace();
        row.Semester!.SemesterCode.Should().Be("SP24", "SE170001 started in the SP24 term");
    }

    [SkippableFact]
    public async Task Both_Prerequisite_relationships_point_at_the_right_course()
    {
        var db = RequireDb();

        // Prerequisite has TWO foreign keys into Course. If the EF configuration
        // swaps their direction, this query returns the wrong course without
        // raising any error at all.
        var prn222 = await db.Prerequisites
            .Include(p => p.Course)
            .Include(p => p.RequiredCourse)
            .Where(p => p.Course!.CourseCode == "PRN222")
            .SingleAsync();

        prn222.RequiredCourse!.CourseCode.Should().Be("PRN212");
    }

    [SkippableFact]
    public async Task Listing_materials_does_not_pull_the_file_content()
    {
        var db = RequireDb();

        // The whole reason Material and MaterialFile are separate tables is so
        // that list queries never touch varbinary(max). If anyone merges them
        // back together, or adds Include(File) to a list query, this test fails.
        var materials = await db.Materials
            .Include(m => m.Course)
            .OrderBy(m => m.MaterialId)
            .ToListAsync();

        materials.Should().HaveCount(8);
        materials.Should().OnlyContain(m => m.File == null,
            "a list query must not load the binary payload");

        // General materials are not attached to any course.
        materials.Should().Contain(m => m.CourseId == null);
    }

    [SkippableFact]
    public async Task Download_returns_the_content_and_the_size_matches()
    {
        var db = RequireDb();

        var material = await db.Materials
            .Include(m => m.File)
            .FirstAsync(m => m.FileName == "PRN212-WPF-MVVM.txt");

        material.File.Should().NotBeNull();
        material.File!.Content.Should().NotBeEmpty();

        // FileSizeBytes in the metadata must match the real payload length,
        // otherwise the download progress indicator lies.
        material.File.Content.LongLength.Should().Be(material.FileSizeBytes);
    }
}
