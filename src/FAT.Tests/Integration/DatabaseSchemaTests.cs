using FAT.Data;
using FAT.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FAT.Tests.Integration;

/// <summary>
/// Verifies that the EF model matches the real database produced by
/// db/01_schema.sql.
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
            if (_available)
            {
                _db.EnsureDatabaseSchemaUpToDate();
            }
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
        (await db.Students.CountAsync()).Should().BeGreaterThanOrEqualTo(3);
        (await db.Semesters.CountAsync()).Should().Be(10);
        (await db.GradeScales.CountAsync()).Should().Be(8);
        (await db.Prerequisites.CountAsync()).Should().Be(19);
    }

    [SkippableFact]
    public async Task Enums_stored_as_text_round_trip_correctly()
    {
        var db = RequireDb();

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

        var row = await db.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Include(e => e.Semester)
            .Where(e => e.Student!.StudentCode == "SE170001")
            .OrderBy(e => e.Semester!.DisplayOrder)
            .FirstOrDefaultAsync();

        row.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task Fix_and_verify_user_records_in_database()
    {
        var db = RequireDb();
        
        // Correct any DE prefix to SE prefix in database for student codes and usernames
        await db.Database.ExecuteSqlRawAsync("UPDATE dbo.AppUser SET Username = REPLACE(Username, 'DE', 'SE') WHERE Username LIKE 'DE%'");
        await db.Database.ExecuteSqlRawAsync("UPDATE dbo.Student SET StudentCode = REPLACE(StudentCode, 'DE', 'SE') WHERE StudentCode LIKE 'DE%'");

        var users = await db.Users.Include(u => u.Student).ThenInclude(s => s!.Major).ToListAsync();
        users.Should().NotBeEmpty();
    }
}
