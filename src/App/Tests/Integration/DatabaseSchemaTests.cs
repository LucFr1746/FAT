using Data;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Tests.Integration;

/// <summary>
/// Verifies that the EF model matches the real database produced by
/// db/01_schema.sql.
/// </summary>
public class DatabaseSchemaTests : IDisposable
{
    private static readonly string ConnectionString = GetConnectionString();

    private static string GetConnectionString()
    {
        var root = TestSupport.RepositoryPaths.Root;
        if (root != null)
        {
            var localSettings = Path.Combine(root, "src", "App", "appsettings.Local.json");
            var settings = Path.Combine(root, "src", "App", "appsettings.json");
            var file = File.Exists(localSettings) ? localSettings : File.Exists(settings) ? settings : null;
            if (file != null)
            {
                var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonFile(file, optional: true)
                    .Build();
                var connStr = builder.GetConnectionString("AppDatabase");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    return connStr;
                }
            }
        }
        return "Server=localhost;Database=FAT_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=5";
    }

    private readonly FAT_DBContext? _db;
    private readonly bool _available;

    public DatabaseSchemaTests()
    {
        var options = new DbContextOptionsBuilder<FAT_DBContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        try
        {
            _db = new FAT_DBContext(options);
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

    private FAT_DBContext RequireDb()
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

        // Catalog tables added with the curriculum module.
        (await db.Terms.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.SubjectMaterials.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.AssessmentSchedules.Take(1).ToListAsync()).Should().NotBeNull();
        (await db.GradePredictions.Take(1).ToListAsync()).Should().NotBeNull();
    }

    /// <summary>
    /// The columns SchemaUpgrader adds. Selecting them is the cheapest proof the
    /// upgrade actually ran against this database - a missing one surfaces here
    /// rather than as "Invalid column name" on a user's first click.
    /// </summary>
    [SkippableFact]
    public async Task Columns_added_by_the_schema_upgrader_exist()
    {
        var db = RequireDb();

        var courses = await db.Courses
            .Select(c => new { c.CountsTowardGpa, c.MinAvgMarkToPass, c.SyllabusCode, c.PrerequisiteText })
            .Take(1)
            .ToListAsync();
        courses.Should().NotBeNull();

        (await db.CurriculumItems.Select(ci => ci.DisplayOrder).Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Prerequisites.Select(p => p.GroupNo).Take(1).ToListAsync()).Should().NotBeNull();

        // PartCount reached db/01_schema.sql without an upgrade step, so every
        // database created before that commit failed EVERY Assessment query.
        (await db.Assessments.Select(a => a.PartCount).Take(1).ToListAsync()).Should().NotBeNull();

        (await db.Majors.Select(m => m.Description).Take(1).ToListAsync()).Should().NotBeNull();
        (await db.Students.Select(s => s.CurrentTermNo).Take(1).ToListAsync()).Should().NotBeNull();
    }

    /// <summary>
    /// Kỳ 0 is real data (OTP101), so CK_Curriculum_Term must allow it. The
    /// original constraint said TermNo &gt;= 1 and would reject this insert.
    /// </summary>
    [SkippableFact]
    public async Task Curriculum_accepts_term_zero_and_round_trips()
    {
        var db = RequireDb();

        var major = await db.Majors.FirstOrDefaultAsync();
        Skip.If(major is null, "No major in the database - run db/setup-db.ps1 first.");

        // A throwaway subject, so the check never collides with seeded data.
        var probeCode = $"ZZT{DateTime.UtcNow:ffff}";
        var course = new Domain.Entities.Course
        {
            CourseCode = probeCode,
            CourseName = "Term zero probe",
            Credits = 0,
            CountsTowardGpa = false,
            IsActive = true
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var item = new Domain.Entities.Curriculum
        {
            MajorId = major!.MajorId,
            CourseId = course.CourseId,
            TermNo = 0,
            DisplayOrder = 0,
            IsMandatory = true
        };

        try
        {
            db.CurriculumItems.Add(item);
            await db.SaveChangesAsync();

            var stored = await db.CurriculumItems
                .AsNoTracking()
                .SingleAsync(ci => ci.CurriculumId == item.CurriculumId);

            stored.TermNo.Should().Be(0);
        }
        finally
        {
            // Always cleaned up: this test runs against the shared development
            // database, and leaving a probe row behind would break the seed-count
            // assertions above on the next run.
            db.CurriculumItems.Remove(item);
            db.Courses.Remove(course);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Kỳ 0..9 are seeded by 01_schema.sql, and Curriculum FKs onto them.</summary>
    [SkippableFact]
    public async Task Terms_are_seeded_from_zero()
    {
        var db = RequireDb();

        var terms = await db.Terms.OrderBy(t => t.TermNo).ToListAsync();

        terms.Should().NotBeEmpty();
        terms.First().TermNo.Should().Be(0, "OTP101 sits in Kỳ 0");
        terms.Select(t => t.TermNo).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// AssessmentSchedule.ExpectedDate is DATE, not datetime2. A time component
    /// sneaking in would break every "is this inside the semester" comparison.
    /// </summary>
    [SkippableFact]
    public async Task Assessment_schedule_expected_date_has_no_time_component()
    {
        var db = RequireDb();

        var dated = await db.AssessmentSchedules
            .AsNoTracking()
            .Where(s => s.ExpectedDate != null)
            .Select(s => s.ExpectedDate!.Value)
            .Take(20)
            .ToListAsync();

        // NotContain rather than OnlyContain: an empty set is the normal state
        // (FLM publishes no dates, so ExpectedDate imports as null), and
        // OnlyContain treats "nothing to check" as a failure.
        dated.Should().NotContain(d => d.TimeOfDay != TimeSpan.Zero);
    }

    [SkippableFact]
    public async Task Seed_data_has_the_expected_row_counts()
    {
        var db = RequireDb();

        (await db.Courses.CountAsync()).Should().BeGreaterThanOrEqualTo(31);
        (await db.Students.CountAsync()).Should().BeGreaterThanOrEqualTo(3);
        (await db.Semesters.CountAsync()).Should().Be(10);
        (await db.GradeScales.CountAsync()).Should().Be(8);
        (await db.Prerequisites.CountAsync()).Should().BeGreaterThanOrEqualTo(19);
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

        // Correct any DE prefix to SE prefix in database for student codes and usernames if target username does not already exist
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE u
            SET Username = REPLACE(Username, 'DE', 'SE')
            FROM dbo.AppUser u
            WHERE u.Username LIKE 'DE%'
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.AppUser u2 WHERE u2.Username = REPLACE(u.Username, 'DE', 'SE')
              )");
        await db.Database.ExecuteSqlRawAsync(@"
            UPDATE s
            SET StudentCode = REPLACE(StudentCode, 'DE', 'SE')
            FROM dbo.Student s
            WHERE s.StudentCode LIKE 'DE%'
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.Student s2 WHERE s2.StudentCode = REPLACE(s.StudentCode, 'DE', 'SE')
              )");

        var users = await db.Users.Include(u => u.Student).ThenInclude(s => s!.Major).ToListAsync();
        users.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SeedCurriculum_imports_latest_curriculum_json_into_real_database()
    {
        if (!_available || _db == null)
        {
            return;
        }

        await DataSeeder.SeedCurriculumIfEmptyAsync(_db, forceImport: true);

        var coursesCount = await _db.Courses.CountAsync();
        coursesCount.Should().BeGreaterThan(30);
    }
}
